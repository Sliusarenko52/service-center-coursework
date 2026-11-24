using Microsoft.Data.Sqlite;
using ServiceCenter.Web.Data;
using ServiceCenter.Web.Models;

namespace ServiceCenter.Web.Services;

/// <summary>
/// Прикладний сервіс керування заявками сервісного центру, роботами, деталями та історією станів.
/// </summary>
public sealed class RequestService
{
    private readonly SqliteConnectionFactory _db;

    public RequestService(SqliteConnectionFactory db)
    {
        _db = db;
    }

    public PagedResult<ServiceRequestSummaryDto> GetRequests(
        long userId,
        string role,
        string? status,
        string? search,
        string? sortBy,
        int page = 1,
        int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var orderClause = sortBy switch
        {
            "created_at" => "r.created_at DESC, r.id DESC",
            "total_cost" => "r.total_cost DESC, r.id DESC",
            "status" => "r.status ASC, r.id DESC",
            _ => "r.updated_at DESC, r.id DESC"
        };

        var conditions = new List<string>();
        if (role == UserRoles.Client)
            conditions.Add("r.client_id = @uid");
        else if (role == UserRoles.Technician)
            conditions.Add("r.technician_id = @uid");

        if (!string.IsNullOrWhiteSpace(status))
            conditions.Add("r.status = @status");

        if (!string.IsNullOrWhiteSpace(search))
            conditions.Add("(lower(r.request_number) LIKE @q OR lower(e.brand || ' ' || e.model) LIKE @q OR lower(e.serial_number) LIKE @q OR lower(c.full_name) LIKE @q OR lower(r.issue_description) LIKE @q)");

        var whereSql = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;

        using var conn = _db.CreateConnection();
        using var countCmd = conn.CreateCommand();
        countCmd.CommandText = $"""
            SELECT COUNT(*)
            FROM service_requests r
            JOIN users c ON c.id = r.client_id
            JOIN equipment e ON e.id = r.equipment_id
            {whereSql};
            """;
        BindFilterParams(countCmd, userId, status, search);
        var totalCount = Convert.ToInt32(countCmd.ExecuteScalar());

        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT r.id, r.request_number, r.client_id, c.full_name,
                   e.category || ' ' || e.brand || ' ' || e.model AS eq_title,
                   e.serial_number, r.technician_id, t.full_name,
                   r.status, r.total_cost, r.version, r.created_at, r.updated_at
            FROM service_requests r
            JOIN users c ON c.id = r.client_id
            JOIN equipment e ON e.id = r.equipment_id
            LEFT JOIN users t ON t.id = r.technician_id
            {whereSql}
            ORDER BY {orderClause}
            LIMIT @limit OFFSET @offset;
            """;
        BindFilterParams(cmd, userId, status, search);
        cmd.Parameters.AddWithValue("@limit", pageSize);
        cmd.Parameters.AddWithValue("@offset", (page - 1) * pageSize);

        var items = new List<ServiceRequestSummaryDto>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            items.Add(new ServiceRequestSummaryDto(
                r.GetInt64(0),
                r.GetString(1),
                r.GetInt64(2),
                r.GetString(3),
                r.GetString(4),
                r.GetString(5),
                r.IsDBNull(6) ? null : r.GetInt64(6),
                r.IsDBNull(7) ? null : r.GetString(7),
                r.GetString(8),
                Convert.ToDecimal(r.GetDouble(9)),
                r.GetInt32(10),
                DateTime.Parse(r.GetString(11)).ToUniversalTime(),
                DateTime.Parse(r.GetString(12)).ToUniversalTime()
            ));
        }

        return new PagedResult<ServiceRequestSummaryDto>(items, totalCount, page, pageSize);
    }

    private static void BindFilterParams(SqliteCommand cmd, long userId, string? status, string? search)
    {
        cmd.Parameters.AddWithValue("@uid", userId);
        if (!string.IsNullOrWhiteSpace(status))
            cmd.Parameters.AddWithValue("@status", status.Trim());
        if (!string.IsNullOrWhiteSpace(search))
            cmd.Parameters.AddWithValue("@q", $"%{search.Trim().ToLowerInvariant()}%");
    }

    public ServiceRequestDetailsDto GetRequestDetails(long id, long userId, string role)
    {
        using var conn = _db.CreateConnection();
        var req = LoadRequestOrThrow(conn, id);
        if (!WorkflowRules.CanView(userId, role, req))
            throw new ForbiddenAppException("У вас немає доступу до перегляду цієї заявки.");

        return BuildDetailsDto(conn, req);
    }

    public ServiceRequestDetailsDto CreateRequest(CreateServiceRequestDto dto, long currentUserId, string role)
    {
        var category = (dto.Category ?? string.Empty).Trim();
        var brand = (dto.Brand ?? string.Empty).Trim();
        var model = (dto.Model ?? string.Empty).Trim();
        var serial = (dto.SerialNumber ?? string.Empty).Trim();
        var issue = (dto.IssueDescription ?? string.Empty).Trim();

        var errors = new Dictionary<string, string[]>();
        if (category.Length is < 2 or > 60)
            errors["category"] = ["Категорія обладнання має містити від 2 до 60 символів."];
        if (brand.Length is < 2 or > 60)
            errors["brand"] = ["Виробник має містити від 2 до 60 символів."];
        if (model.Length is < 1 or > 80)
            errors["model"] = ["Модель має містити від 1 до 80 символів."];
        if (serial.Length is < 3 or > 60)
            errors["serialNumber"] = ["Серійний номер має містити від 3 до 60 символів."];
        if (issue.Length is < 5 or > 1000)
            errors["issueDescription"] = ["Опис несправності має містити від 5 до 1000 символів."];

        if (role == UserRoles.Technician)
            throw new ForbiddenAppException("Виконавець не може самостійно створювати нові заявки.");

        long targetClientId = currentUserId;
        if (role == UserRoles.Receiver)
        {
            if (dto.ClientId is null or <= 0)
                errors["clientId"] = ["Приймальник повинен обрати клієнта для заявки."];
            else
                targetClientId = dto.ClientId.Value;
        }

        if (errors.Count > 0)
            throw new ValidationAppException("Перевірте коректність заповнення форми заявки.", errors);

        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        if (role == UserRoles.Receiver)
        {
            using var clientCheck = conn.CreateCommand();
            clientCheck.Transaction = tx;
            clientCheck.CommandText = "SELECT COUNT(*) FROM users WHERE id = @cid AND role = 'CLIENT';";
            clientCheck.Parameters.AddWithValue("@cid", targetClientId);
            if (Convert.ToInt64(clientCheck.ExecuteScalar()) == 0)
                throw new ValidationAppException("Обраного клієнта не знайдено.");
        }

        var nowIso = DateTime.UtcNow.ToString("O");

        using var eqCmd = conn.CreateCommand();
        eqCmd.Transaction = tx;
        eqCmd.CommandText = """
            INSERT INTO equipment (client_id, category, brand, model, serial_number, created_at)
            VALUES (@cid, @cat, @brand, @model, @sn, @at);
            SELECT last_insert_rowid();
            """;
        eqCmd.Parameters.AddWithValue("@cid", targetClientId);
        eqCmd.Parameters.AddWithValue("@cat", category);
        eqCmd.Parameters.AddWithValue("@brand", brand);
        eqCmd.Parameters.AddWithValue("@model", model);
        eqCmd.Parameters.AddWithValue("@sn", serial);
        eqCmd.Parameters.AddWithValue("@at", nowIso);
        var eqId = Convert.ToInt64(eqCmd.ExecuteScalar());

        var tempNum = "SR-TMP-" + Guid.NewGuid().ToString("N")[..8];
        using var reqCmd = conn.CreateCommand();
        reqCmd.Transaction = tx;
        reqCmd.CommandText = """
            INSERT INTO service_requests
            (request_number, client_id, equipment_id, technician_id, issue_description,
             diagnostics_notes, completion_result, status, total_cost, version, created_at, updated_at)
            VALUES
            (@num, @cid, @eid, NULL, @issue, '', '', 'NEW', 0, 1, @at, @at);
            SELECT last_insert_rowid();
            """;
        reqCmd.Parameters.AddWithValue("@num", tempNum);
        reqCmd.Parameters.AddWithValue("@cid", targetClientId);
        reqCmd.Parameters.AddWithValue("@eid", eqId);
        reqCmd.Parameters.AddWithValue("@issue", issue);
        reqCmd.Parameters.AddWithValue("@at", nowIso);
        var newReqId = Convert.ToInt64(reqCmd.ExecuteScalar());

        var finalNum = $"SR-{DateTime.UtcNow.Year}-{newReqId:D4}";
        using var updNum = conn.CreateCommand();
        updNum.Transaction = tx;
        updNum.CommandText = "UPDATE service_requests SET request_number = @num WHERE id = @id;";
        updNum.Parameters.AddWithValue("@num", finalNum);
        updNum.Parameters.AddWithValue("@id", newReqId);
        updNum.ExecuteNonQuery();

        InsertHistory(conn, tx, newReqId, null, RequestStatuses.New, currentUserId, "Заявку зареєстровано", nowIso);

        tx.Commit();

        var created = LoadRequestOrThrow(conn, newReqId);
        return BuildDetailsDto(conn, created);
    }

    public ServiceRequestDetailsDto AssignTechnician(long requestId, AssignTechnicianDto dto, long currentUserId, string role)
    {
        if (role != UserRoles.Receiver)
            throw new ForbiddenAppException("Призначати виконавця може лише приймальник.");
        if (dto.TechnicianId <= 0)
            throw new ValidationAppException("Оберіть коректного виконавця.");

        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        var req = LoadRequestOrThrow(conn, requestId, tx);
        if (req.Status is not (RequestStatuses.New or RequestStatuses.Assigned))
        {
            throw new ConflictAppException(
                $"Призначення виконавця неможливе для заявки у стані «{req.Status}».",
                "INVALID_STATE",
                req.Version);
        }

        using var techCmd = conn.CreateCommand();
        techCmd.Transaction = tx;
        techCmd.CommandText = "SELECT full_name FROM users WHERE id = @tid AND role = 'TECHNICIAN' LIMIT 1;";
        techCmd.Parameters.AddWithValue("@tid", dto.TechnicianId);
        var techName = techCmd.ExecuteScalar() as string
            ?? throw new ValidationAppException("Обраного працівника з роллю виконавця не знайдено.");

        var nowIso = DateTime.UtcNow.ToString("O");
        var newStatus = RequestStatuses.Assigned;

        using var upd = conn.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = """
            UPDATE service_requests
            SET technician_id = @tid, status = @st, version = version + 1, updated_at = @at
            WHERE id = @id AND version = @ver;
            """;
        upd.Parameters.AddWithValue("@tid", dto.TechnicianId);
        upd.Parameters.AddWithValue("@st", newStatus);
        upd.Parameters.AddWithValue("@at", nowIso);
        upd.Parameters.AddWithValue("@id", requestId);
        upd.Parameters.AddWithValue("@ver", dto.Version);

        EnsureOptimisticLock(conn, tx, requestId, upd.ExecuteNonQuery());

        InsertHistory(conn, tx, requestId, req.Status, newStatus, currentUserId, $"Призначено виконавця: {techName}", nowIso);
        tx.Commit();

        return BuildDetailsDto(conn, LoadRequestOrThrow(conn, requestId));
    }

    public ServiceRequestDetailsDto UpdateDiagnostics(long requestId, UpdateDiagnosticsDto dto, long currentUserId, string role)
    {
        var notes = (dto.DiagnosticsNotes ?? string.Empty).Trim();
        if (notes.Length is < 3 or > 2000)
            throw new ValidationAppException("Результати діагностики мають містити від 3 до 2000 символів.");

        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        var req = LoadRequestOrThrow(conn, requestId, tx);
        if (!WorkflowRules.CanView(currentUserId, role, req))
            throw new ForbiddenAppException();
        if (!WorkflowRules.CanEditDiagnosticsOrEstimate(req, role, currentUserId))
            throw new ConflictAppException("Редагування діагностики недоступне у поточному стані або для вашої ролі.", "CANNOT_EDIT", req.Version);

        var nowIso = DateTime.UtcNow.ToString("O");
        var nextStatus = req.Status == RequestStatuses.Assigned ? RequestStatuses.Diagnosing : req.Status;

        using var upd = conn.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = """
            UPDATE service_requests
            SET diagnostics_notes = @notes, status = @st, version = version + 1, updated_at = @at
            WHERE id = @id AND version = @ver;
            """;
        upd.Parameters.AddWithValue("@notes", notes);
        upd.Parameters.AddWithValue("@st", nextStatus);
        upd.Parameters.AddWithValue("@at", nowIso);
        upd.Parameters.AddWithValue("@id", requestId);
        upd.Parameters.AddWithValue("@ver", dto.Version);

        EnsureOptimisticLock(conn, tx, requestId, upd.ExecuteNonQuery());

        if (nextStatus != req.Status)
            InsertHistory(conn, tx, requestId, req.Status, nextStatus, currentUserId, "Зафіксовано результати діагностики", nowIso);

        tx.Commit();
        return BuildDetailsDto(conn, LoadRequestOrThrow(conn, requestId));
    }

    public ServiceRequestDetailsDto AddWorkItem(long requestId, AddWorkItemDto dto, long currentUserId, string role)
    {
        var title = (dto.Title ?? string.Empty).Trim();
        var errors = new Dictionary<string, string[]>();
        if (title.Length is < 3 or > 150)
            errors["title"] = ["Назва виду роботи має містити від 3 до 150 символів."];
        if (dto.Price <= 0 || dto.Price > 1_000_000m)
            errors["price"] = ["Вартість роботи має бути більшою за 0 та не перевищувати 1 000 000 грн."];
        if (errors.Count > 0)
            throw new ValidationAppException("Некоректні параметри виду роботи.", errors);

        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        var req = LoadRequestOrThrow(conn, requestId, tx);
        if (!WorkflowRules.CanView(currentUserId, role, req))
            throw new ForbiddenAppException();
        if (!WorkflowRules.CanEditDiagnosticsOrEstimate(req, role, currentUserId))
            throw new ConflictAppException("Додавання робіт недоступне у поточному стані заявки.", "CANNOT_EDIT", req.Version);

        var nowIso = DateTime.UtcNow.ToString("O");

        using var ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = """
            INSERT INTO work_items (request_id, title, price, is_approved, created_by_id, created_at)
            VALUES (@rid, @title, @price, 0, @uid, @at);
            """;
        ins.Parameters.AddWithValue("@rid", requestId);
        ins.Parameters.AddWithValue("@title", title);
        ins.Parameters.AddWithValue("@price", (double)dto.Price);
        ins.Parameters.AddWithValue("@uid", currentUserId);
        ins.Parameters.AddWithValue("@at", nowIso);
        ins.ExecuteNonQuery();

        var newTotal = RecalculateTotalCost(conn, tx, requestId);
        BumpVersionAndCost(conn, tx, requestId, dto.Version, newTotal, nowIso);

        tx.Commit();
        return BuildDetailsDto(conn, LoadRequestOrThrow(conn, requestId));
    }

    public ServiceRequestDetailsDto RemoveWorkItem(long requestId, long workItemId, int expectedVersion, long currentUserId, string role)
    {
        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        var req = LoadRequestOrThrow(conn, requestId, tx);
        if (!WorkflowRules.CanView(currentUserId, role, req))
            throw new ForbiddenAppException();
        if (!WorkflowRules.CanEditDiagnosticsOrEstimate(req, role, currentUserId))
            throw new ConflictAppException("Видалення робіт недоступне у поточному стані заявки.", "CANNOT_EDIT", req.Version);

        using var del = conn.CreateCommand();
        del.Transaction = tx;
        del.CommandText = "DELETE FROM work_items WHERE id = @wid AND request_id = @rid;";
        del.Parameters.AddWithValue("@wid", workItemId);
        del.Parameters.AddWithValue("@rid", requestId);
        if (del.ExecuteNonQuery() == 0)
            throw new NotFoundAppException("Позицію роботи не знайдено.");

        var nowIso = DateTime.UtcNow.ToString("O");
        var newTotal = RecalculateTotalCost(conn, tx, requestId);
        BumpVersionAndCost(conn, tx, requestId, expectedVersion, newTotal, nowIso);

        tx.Commit();
        return BuildDetailsDto(conn, LoadRequestOrThrow(conn, requestId));
    }

    public ServiceRequestDetailsDto AddRequestPart(long requestId, AddRequestPartDto dto, long currentUserId, string role)
    {
        if (dto.PartId <= 0 || dto.Quantity is < 1 or > 100)
            throw new ValidationAppException("Вкажіть коректну деталь та кількість (від 1 до 100 шт.).");

        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        var req = LoadRequestOrThrow(conn, requestId, tx);
        if (!WorkflowRules.CanView(currentUserId, role, req))
            throw new ForbiddenAppException();
        if (!WorkflowRules.CanEditDiagnosticsOrEstimate(req, role, currentUserId))
            throw new ConflictAppException("Додавання деталей недоступне у поточному стані заявки.", "CANNOT_EDIT", req.Version);

        using var partCmd = conn.CreateCommand();
        partCmd.Transaction = tx;
        partCmd.CommandText = "SELECT name, unit_price, stock_qty FROM parts WHERE id = @pid LIMIT 1;";
        partCmd.Parameters.AddWithValue("@pid", dto.PartId);
        using var pr = partCmd.ExecuteReader();
        if (!pr.Read())
            throw new NotFoundAppException("Деталь на складі не знайдено.");

        var partName = pr.GetString(0);
        var unitPrice = Convert.ToDecimal(pr.GetDouble(1));
        var stockQty = pr.GetInt32(2);
        pr.Close();

        if (stockQty < dto.Quantity)
            throw new ConflictAppException($"Недостатньо деталей «{partName}» на складі (доступно: {stockQty} шт.).", "INSUFFICIENT_STOCK", req.Version);

        using var stockUpd = conn.CreateCommand();
        stockUpd.Transaction = tx;
        stockUpd.CommandText = "UPDATE parts SET stock_qty = stock_qty - @qty WHERE id = @pid AND stock_qty >= @qty;";
        stockUpd.Parameters.AddWithValue("@qty", dto.Quantity);
        stockUpd.Parameters.AddWithValue("@pid", dto.PartId);
        if (stockUpd.ExecuteNonQuery() == 0)
            throw new ConflictAppException("Недостатньо залишку деталі на складі.", "INSUFFICIENT_STOCK", req.Version);

        var nowIso = DateTime.UtcNow.ToString("O");
        using var ins = conn.CreateCommand();
        ins.Transaction = tx;
        ins.CommandText = """
            INSERT INTO request_parts (request_id, part_id, part_name, quantity, unit_price, created_by_id, created_at)
            VALUES (@rid, @pid, @pname, @qty, @price, @uid, @at);
            """;
        ins.Parameters.AddWithValue("@rid", requestId);
        ins.Parameters.AddWithValue("@pid", dto.PartId);
        ins.Parameters.AddWithValue("@pname", partName);
        ins.Parameters.AddWithValue("@qty", dto.Quantity);
        ins.Parameters.AddWithValue("@price", (double)unitPrice);
        ins.Parameters.AddWithValue("@uid", currentUserId);
        ins.Parameters.AddWithValue("@at", nowIso);
        ins.ExecuteNonQuery();

        var newTotal = RecalculateTotalCost(conn, tx, requestId);
        BumpVersionAndCost(conn, tx, requestId, dto.Version, newTotal, nowIso);

        tx.Commit();
        return BuildDetailsDto(conn, LoadRequestOrThrow(conn, requestId));
    }

    public ServiceRequestDetailsDto RemoveRequestPart(long requestId, long requestPartId, int expectedVersion, long currentUserId, string role)
    {
        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        var req = LoadRequestOrThrow(conn, requestId, tx);
        if (!WorkflowRules.CanView(currentUserId, role, req))
            throw new ForbiddenAppException();
        if (!WorkflowRules.CanEditDiagnosticsOrEstimate(req, role, currentUserId))
            throw new ConflictAppException("Видалення деталей недоступне у поточному стані заявки.", "CANNOT_EDIT", req.Version);

        using var sel = conn.CreateCommand();
        sel.Transaction = tx;
        sel.CommandText = "SELECT part_id, quantity FROM request_parts WHERE id = @rpid AND request_id = @rid LIMIT 1;";
        sel.Parameters.AddWithValue("@rpid", requestPartId);
        sel.Parameters.AddWithValue("@rid", requestId);
        using var r = sel.ExecuteReader();
        if (!r.Read())
            throw new NotFoundAppException("Запис деталі у заявці не знайдено.");
        var partId = r.GetInt64(0);
        var qty = r.GetInt32(1);
        r.Close();

        using var del = conn.CreateCommand();
        del.Transaction = tx;
        del.CommandText = "DELETE FROM request_parts WHERE id = @rpid;";
        del.Parameters.AddWithValue("@rpid", requestPartId);
        del.ExecuteNonQuery();

        using var restore = conn.CreateCommand();
        restore.Transaction = tx;
        restore.CommandText = "UPDATE parts SET stock_qty = stock_qty + @qty WHERE id = @pid;";
        restore.Parameters.AddWithValue("@qty", qty);
        restore.Parameters.AddWithValue("@pid", partId);
        restore.ExecuteNonQuery();

        var nowIso = DateTime.UtcNow.ToString("O");
        var newTotal = RecalculateTotalCost(conn, tx, requestId);
        BumpVersionAndCost(conn, tx, requestId, expectedVersion, newTotal, nowIso);

        tx.Commit();
        return BuildDetailsDto(conn, LoadRequestOrThrow(conn, requestId));
    }

    public ServiceRequestDetailsDto ChangeStatus(long requestId, ChangeStatusDto dto, long currentUserId, string role)
    {
        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        var req = LoadRequestOrThrow(conn, requestId, tx);
        if (!WorkflowRules.CanView(currentUserId, role, req))
            throw new ForbiddenAppException();

        using var wCountCmd = conn.CreateCommand();
        wCountCmd.Transaction = tx;
        wCountCmd.CommandText = "SELECT COUNT(*), COALESCE(SUM(CASE WHEN is_approved = 0 THEN 1 ELSE 0 END), 0) FROM work_items WHERE request_id = @rid;";
        wCountCmd.Parameters.AddWithValue("@rid", requestId);
        using var wr = wCountCmd.ExecuteReader();
        wr.Read();
        var workCount = wr.GetInt32(0);
        var unapprovedCount = wr.GetInt32(1);
        wr.Close();

        WorkflowRules.ValidateStatusTransition(
            req, dto.NewStatus, role, currentUserId,
            dto.CompletionResult, dto.Comment, workCount, unapprovedCount);

        var nowIso = DateTime.UtcNow.ToString("O");
        var finalCompletion = !string.IsNullOrWhiteSpace(dto.CompletionResult)
            ? dto.CompletionResult.Trim()
            : req.CompletionResult;

        if (dto.NewStatus == RequestStatuses.Approved)
        {
            using var appCmd = conn.CreateCommand();
            appCmd.Transaction = tx;
            appCmd.CommandText = "UPDATE work_items SET is_approved = 1 WHERE request_id = @rid;";
            appCmd.Parameters.AddWithValue("@rid", requestId);
            appCmd.ExecuteNonQuery();
        }

        using var upd = conn.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = """
            UPDATE service_requests
            SET status = @st, completion_result = @res, version = version + 1, updated_at = @at
            WHERE id = @id AND version = @ver;
            """;
        upd.Parameters.AddWithValue("@st", dto.NewStatus);
        upd.Parameters.AddWithValue("@res", finalCompletion);
        upd.Parameters.AddWithValue("@at", nowIso);
        upd.Parameters.AddWithValue("@id", requestId);
        upd.Parameters.AddWithValue("@ver", dto.Version);

        EnsureOptimisticLock(conn, tx, requestId, upd.ExecuteNonQuery());

        var historyComment = string.IsNullOrWhiteSpace(dto.Comment)
            ? $"Статус змінено на {dto.NewStatus}"
            : dto.Comment.Trim();
        InsertHistory(conn, tx, requestId, req.Status, dto.NewStatus, currentUserId, historyComment, nowIso);

        tx.Commit();
        return BuildDetailsDto(conn, LoadRequestOrThrow(conn, requestId));
    }

    public IReadOnlyList<PartRecord> GetParts()
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, sku, name, unit_price, stock_qty FROM parts ORDER BY name;";
        var list = new List<PartRecord>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new PartRecord
            {
                Id = r.GetInt64(0),
                Sku = r.GetString(1),
                Name = r.GetString(2),
                UnitPrice = Convert.ToDecimal(r.GetDouble(3)),
                StockQty = r.GetInt32(4)
            });
        }
        return list;
    }

    private static ServiceRequestRecord LoadRequestOrThrow(SqliteConnection conn, long id, SqliteTransaction? tx = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT id, request_number, client_id, equipment_id, technician_id,
                   issue_description, diagnostics_notes, completion_result,
                   status, total_cost, version, created_at, updated_at
            FROM service_requests
            WHERE id = @id
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@id", id);

        using var r = cmd.ExecuteReader();
        if (!r.Read())
            throw new NotFoundAppException($"Заявку з ідентифікатором {id} не знайдено.");

        return new ServiceRequestRecord
        {
            Id = r.GetInt64(0),
            RequestNumber = r.GetString(1),
            ClientId = r.GetInt64(2),
            EquipmentId = r.GetInt64(3),
            TechnicianId = r.IsDBNull(4) ? null : r.GetInt64(4),
            IssueDescription = r.GetString(5),
            DiagnosticsNotes = r.GetString(6),
            CompletionResult = r.GetString(7),
            Status = r.GetString(8),
            TotalCost = Convert.ToDecimal(r.GetDouble(9)),
            Version = r.GetInt32(10),
            CreatedAt = DateTime.Parse(r.GetString(11)).ToUniversalTime(),
            UpdatedAt = DateTime.Parse(r.GetString(12)).ToUniversalTime()
        };
    }

    private static void EnsureOptimisticLock(SqliteConnection conn, SqliteTransaction tx, long requestId, int rowsAffected)
    {
        if (rowsAffected > 0) return;

        using var verCmd = conn.CreateCommand();
        verCmd.Transaction = tx;
        verCmd.CommandText = "SELECT version FROM service_requests WHERE id = @id;";
        verCmd.Parameters.AddWithValue("@id", requestId);
        var currentVer = Convert.ToInt32(verCmd.ExecuteScalar());

        throw new ConflictAppException(
            $"Заявку вже було змінено іншим працівником (актуальна версія: v{currentVer}). Оновіть дані перед збереженням.",
            "VERSION_CONFLICT",
            currentVer);
    }

    private static decimal RecalculateTotalCost(SqliteConnection conn, SqliteTransaction tx, long requestId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            SELECT
                (SELECT COALESCE(SUM(price), 0) FROM work_items WHERE request_id = @rid) +
                (SELECT COALESCE(SUM(quantity * unit_price), 0) FROM request_parts WHERE request_id = @rid);
            """;
        cmd.Parameters.AddWithValue("@rid", requestId);
        return Convert.ToDecimal(cmd.ExecuteScalar());
    }

    private static void BumpVersionAndCost(SqliteConnection conn, SqliteTransaction tx, long requestId, int expectedVersion, decimal totalCost, string nowIso)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            UPDATE service_requests
            SET total_cost = @cost, version = version + 1, updated_at = @at
            WHERE id = @id AND version = @ver;
            """;
        cmd.Parameters.AddWithValue("@cost", (double)totalCost);
        cmd.Parameters.AddWithValue("@at", nowIso);
        cmd.Parameters.AddWithValue("@id", requestId);
        cmd.Parameters.AddWithValue("@ver", expectedVersion);

        EnsureOptimisticLock(conn, tx, requestId, cmd.ExecuteNonQuery());
    }

    private static void InsertHistory(
        SqliteConnection conn, SqliteTransaction tx, long requestId,
        string? fromStatus, string toStatus, long userId, string comment, string nowIso)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO request_status_history (request_id, from_status, to_status, changed_by_id, comment, changed_at)
            VALUES (@rid, @from, @to, @uid, @comment, @at);
            """;
        cmd.Parameters.AddWithValue("@rid", requestId);
        cmd.Parameters.AddWithValue("@from", (object?)fromStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@to", toStatus);
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@comment", comment);
        cmd.Parameters.AddWithValue("@at", nowIso);
        cmd.ExecuteNonQuery();
    }

    private static ServiceRequestDetailsDto BuildDetailsDto(SqliteConnection conn, ServiceRequestRecord req)
    {
        string clientName = string.Empty, clientPhone = string.Empty;
        using (var cCmd = conn.CreateCommand())
        {
            cCmd.CommandText = "SELECT full_name, phone FROM users WHERE id = @id;";
            cCmd.Parameters.AddWithValue("@id", req.ClientId);
            using var r = cCmd.ExecuteReader();
            if (r.Read()) { clientName = r.GetString(0); clientPhone = r.GetString(1); }
        }

        string? techName = null;
        if (req.TechnicianId.HasValue)
        {
            using var tCmd = conn.CreateCommand();
            tCmd.CommandText = "SELECT full_name FROM users WHERE id = @id;";
            tCmd.Parameters.AddWithValue("@id", req.TechnicianId.Value);
            techName = tCmd.ExecuteScalar() as string;
        }

        string cat = "", brand = "", model = "", sn = "";
        using (var eCmd = conn.CreateCommand())
        {
            eCmd.CommandText = "SELECT category, brand, model, serial_number FROM equipment WHERE id = @id;";
            eCmd.Parameters.AddWithValue("@id", req.EquipmentId);
            using var r = eCmd.ExecuteReader();
            if (r.Read()) { cat = r.GetString(0); brand = r.GetString(1); model = r.GetString(2); sn = r.GetString(3); }
        }

        var works = new List<WorkItemDto>();
        decimal worksTotal = 0m;
        using (var wCmd = conn.CreateCommand())
        {
            wCmd.CommandText = "SELECT id, title, price, is_approved, created_at FROM work_items WHERE request_id = @rid ORDER BY id;";
            wCmd.Parameters.AddWithValue("@rid", req.Id);
            using var r = wCmd.ExecuteReader();
            while (r.Read())
            {
                var p = Convert.ToDecimal(r.GetDouble(2));
                worksTotal += p;
                works.Add(new WorkItemDto(r.GetInt64(0), r.GetString(1), p, r.GetInt32(3) == 1, DateTime.Parse(r.GetString(4)).ToUniversalTime()));
            }
        }

        var parts = new List<RequestPartDto>();
        decimal partsTotal = 0m;
        using (var pCmd = conn.CreateCommand())
        {
            pCmd.CommandText = "SELECT id, part_id, part_name, quantity, unit_price, created_at FROM request_parts WHERE request_id = @rid ORDER BY id;";
            pCmd.Parameters.AddWithValue("@rid", req.Id);
            using var r = pCmd.ExecuteReader();
            while (r.Read())
            {
                var qty = r.GetInt32(3);
                var unitPrice = Convert.ToDecimal(r.GetDouble(4));
                var sub = qty * unitPrice;
                partsTotal += sub;
                parts.Add(new RequestPartDto(r.GetInt64(0), r.GetInt64(1), r.GetString(2), qty, unitPrice, sub, DateTime.Parse(r.GetString(5)).ToUniversalTime()));
            }
        }

        var history = new List<StatusHistoryDto>();
        using (var hCmd = conn.CreateCommand())
        {
            hCmd.CommandText = """
                SELECT h.id, h.from_status, h.to_status, h.changed_by_id, u.full_name, h.comment, h.changed_at
                FROM request_status_history h
                JOIN users u ON u.id = h.changed_by_id
                WHERE h.request_id = @rid
                ORDER BY h.id ASC;
                """;
            hCmd.Parameters.AddWithValue("@rid", req.Id);
            using var r = hCmd.ExecuteReader();
            while (r.Read())
            {
                history.Add(new StatusHistoryDto(
                    r.GetInt64(0),
                    r.IsDBNull(1) ? null : r.GetString(1),
                    r.GetString(2),
                    r.GetInt64(3),
                    r.GetString(4),
                    r.GetString(5),
                    DateTime.Parse(r.GetString(6)).ToUniversalTime()
                ));
            }
        }

        return new ServiceRequestDetailsDto(
            req.Id, req.RequestNumber, req.ClientId, clientName, clientPhone,
            req.EquipmentId, cat, brand, model, sn,
            req.TechnicianId, techName,
            req.IssueDescription, req.DiagnosticsNotes, req.CompletionResult,
            req.Status, worksTotal, partsTotal, worksTotal + partsTotal,
            req.Version, req.CreatedAt, req.UpdatedAt,
            works, parts, history
        );
    }
}
