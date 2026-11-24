using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using ServiceCenter.Web.Models;

namespace ServiceCenter.Web.Data;

/// <summary>
/// Фабрика підключень до SQLite з увімкненою перевіркою зовнішніх ключів.
/// </summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        pragma.ExecuteNonQuery();
        return connection;
    }
}

/// <summary>
/// Ініціалізація схеми БД (міграція) та початкове наповнення синтетичними даними.
/// </summary>
public static class DatabaseInitializer
{
    public static void Initialize(SqliteConnectionFactory factory)
    {
        using var conn = factory.CreateConnection();
        using var walCmd = conn.CreateCommand();
        walCmd.CommandText = "PRAGMA journal_mode = WAL;";
        walCmd.ExecuteNonQuery();

        using var ddl = conn.CreateCommand();
        ddl.CommandText = """
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                email TEXT NOT NULL UNIQUE,
                full_name TEXT NOT NULL,
                phone TEXT NOT NULL,
                role TEXT NOT NULL CHECK (role IN ('CLIENT', 'RECEIVER', 'TECHNICIAN')),
                password_hash TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS equipment (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                client_id INTEGER NOT NULL,
                category TEXT NOT NULL,
                brand TEXT NOT NULL,
                model TEXT NOT NULL,
                serial_number TEXT NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (client_id) REFERENCES users(id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS service_requests (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                request_number TEXT NOT NULL UNIQUE,
                client_id INTEGER NOT NULL,
                equipment_id INTEGER NOT NULL,
                technician_id INTEGER NULL,
                issue_description TEXT NOT NULL,
                diagnostics_notes TEXT NOT NULL DEFAULT '',
                completion_result TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL CHECK (status IN (
                    'NEW', 'ASSIGNED', 'DIAGNOSING', 'AWAITING_APPROVAL',
                    'APPROVED', 'IN_PROGRESS', 'WORK_DONE', 'COMPLETED',
                    'REJECTED', 'CANCELLED'
                )),
                total_cost REAL NOT NULL DEFAULT 0 CHECK (total_cost >= 0),
                version INTEGER NOT NULL DEFAULT 1 CHECK (version >= 1),
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                FOREIGN KEY (client_id) REFERENCES users(id) ON DELETE RESTRICT,
                FOREIGN KEY (equipment_id) REFERENCES equipment(id) ON DELETE RESTRICT,
                FOREIGN KEY (technician_id) REFERENCES users(id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS work_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                request_id INTEGER NOT NULL,
                title TEXT NOT NULL,
                price REAL NOT NULL CHECK (price >= 0),
                is_approved INTEGER NOT NULL DEFAULT 0,
                created_by_id INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (request_id) REFERENCES service_requests(id) ON DELETE CASCADE,
                FOREIGN KEY (created_by_id) REFERENCES users(id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS parts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                sku TEXT NOT NULL UNIQUE,
                name TEXT NOT NULL,
                unit_price REAL NOT NULL CHECK (unit_price >= 0),
                stock_qty INTEGER NOT NULL CHECK (stock_qty >= 0)
            );

            CREATE TABLE IF NOT EXISTS request_parts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                request_id INTEGER NOT NULL,
                part_id INTEGER NOT NULL,
                part_name TEXT NOT NULL,
                quantity INTEGER NOT NULL CHECK (quantity > 0),
                unit_price REAL NOT NULL CHECK (unit_price >= 0),
                created_by_id INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                FOREIGN KEY (request_id) REFERENCES service_requests(id) ON DELETE CASCADE,
                FOREIGN KEY (part_id) REFERENCES parts(id) ON DELETE RESTRICT,
                FOREIGN KEY (created_by_id) REFERENCES users(id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS request_status_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                request_id INTEGER NOT NULL,
                from_status TEXT NULL,
                to_status TEXT NOT NULL,
                changed_by_id INTEGER NOT NULL,
                comment TEXT NOT NULL DEFAULT '',
                changed_at TEXT NOT NULL,
                FOREIGN KEY (request_id) REFERENCES service_requests(id) ON DELETE CASCADE,
                FOREIGN KEY (changed_by_id) REFERENCES users(id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS idx_requests_client ON service_requests(client_id);
            CREATE INDEX IF NOT EXISTS idx_requests_tech ON service_requests(technician_id);
            CREATE INDEX IF NOT EXISTS idx_requests_status ON service_requests(status);
            CREATE INDEX IF NOT EXISTS idx_requests_updated ON service_requests(updated_at DESC);
            CREATE INDEX IF NOT EXISTS idx_work_items_request ON work_items(request_id);
            CREATE INDEX IF NOT EXISTS idx_request_parts_request ON request_parts(request_id);
            CREATE INDEX IF NOT EXISTS idx_history_request ON request_status_history(request_id);
            """;
        ddl.ExecuteNonQuery();

        SeedIfEmpty(conn);
    }

    private static void SeedIfEmpty(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM users;";
        var count = Convert.ToInt64(check.ExecuteScalar());
        if (count > 0)
        {
            return;
        }

        using var tx = conn.BeginTransaction();
        var hasher = new PasswordHasher<UserRecord>();
        var now = DateTime.UtcNow;

        long InsertUser(string email, string fullName, string phone, string role, string password)
        {
            var user = new UserRecord { Email = email, FullName = fullName, Phone = phone, Role = role };
            var hash = hasher.HashPassword(user, password);
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO users (email, full_name, phone, role, password_hash, created_at)
                VALUES (@email, @name, @phone, @role, @hash, @created);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@name", fullName);
            cmd.Parameters.AddWithValue("@phone", phone);
            cmd.Parameters.AddWithValue("@role", role);
            cmd.Parameters.AddWithValue("@hash", hash);
            cmd.Parameters.AddWithValue("@created", now.AddDays(-10).ToString("O"));
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        var client1 = InsertUser("client1@service.local", "Петренко Олександр Сергійович", "+380671112233", UserRoles.Client, "ClientPass123456!");
        var client2 = InsertUser("client2@service.local", "Коваленко Марія Іванівна", "+380502223344", UserRoles.Client, "ClientPass123456!");
        var receiver = InsertUser("receiver@service.local", "Іваненко Олена Вікторівна", "+380633334455", UserRoles.Receiver, "ReceiverPass123!");
        var tech1 = InsertUser("tech1@service.local", "Мельник Андрій Петрович", "+380974445566", UserRoles.Technician, "TechPass123456!");
        var tech2 = InsertUser("tech2@service.local", "Шевченко Дмитро Олегович", "+380995556677", UserRoles.Technician, "TechPass123456!");

        long InsertPart(string sku, string name, decimal price, int stock)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO parts (sku, name, unit_price, stock_qty)
                VALUES (@sku, @name, @price, @stock);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@sku", sku);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@price", (double)price);
            cmd.Parameters.AddWithValue("@stock", stock);
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        var partThermal = InsertPart("PRT-001", "Термопаста Arctic MX-4 (4 г)", 280m, 25);
        var partSsd = InsertPart("PRT-002", "SSD NVMe 512 ГБ Kingston", 1650m, 8);
        var partBattery = InsertPart("PRT-003", "Акумулятор для ноутбука 4500 mAh", 1900m, 5);
        var partFan = InsertPart("PRT-004", "Кулер охолодження процесора ноутбука", 650m, 12);
        var partDisplay = InsertPart("PRT-005", "Дисплейний модуль IPS 6.5\"", 2400m, 4);
        var partPort = InsertPart("PRT-006", "Роз'єм живлення USB-C", 320m, 18);

        long InsertEquipment(long clientId, string category, string brand, string model, string serial)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO equipment (client_id, category, brand, model, serial_number, created_at)
                VALUES (@cid, @cat, @brand, @model, @sn, @created);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@cid", clientId);
            cmd.Parameters.AddWithValue("@cat", category);
            cmd.Parameters.AddWithValue("@brand", brand);
            cmd.Parameters.AddWithValue("@model", model);
            cmd.Parameters.AddWithValue("@sn", serial);
            cmd.Parameters.AddWithValue("@created", now.AddDays(-8).ToString("O"));
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        long InsertRequest(
            string number, long clientId, long eqId, long? techId,
            string issue, string diag, string result, string status,
            decimal totalCost, int daysAgo)
        {
            var created = now.AddDays(-daysAgo);
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO service_requests
                (request_number, client_id, equipment_id, technician_id, issue_description,
                 diagnostics_notes, completion_result, status, total_cost, version, created_at, updated_at)
                VALUES
                (@num, @cid, @eid, @tid, @issue, @diag, @res, @status, @cost, 1, @created, @updated);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@num", number);
            cmd.Parameters.AddWithValue("@cid", clientId);
            cmd.Parameters.AddWithValue("@eid", eqId);
            cmd.Parameters.AddWithValue("@tid", (object?)techId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@issue", issue);
            cmd.Parameters.AddWithValue("@diag", diag);
            cmd.Parameters.AddWithValue("@res", result);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@cost", (double)totalCost);
            cmd.Parameters.AddWithValue("@created", created.ToString("O"));
            cmd.Parameters.AddWithValue("@updated", created.AddHours(3).ToString("O"));
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        void AddHistory(long reqId, string? fromSt, string toSt, long userId, string comment, int hoursAgo)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO request_status_history (request_id, from_status, to_status, changed_by_id, comment, changed_at)
                VALUES (@rid, @from, @to, @uid, @comment, @at);
                """;
            cmd.Parameters.AddWithValue("@rid", reqId);
            cmd.Parameters.AddWithValue("@from", (object?)fromSt ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@to", toSt);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@comment", comment);
            cmd.Parameters.AddWithValue("@at", now.AddHours(-hoursAgo).ToString("O"));
            cmd.ExecuteNonQuery();
        }

        void AddWork(long reqId, string title, decimal price, bool approved, long userId)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO work_items (request_id, title, price, is_approved, created_by_id, created_at)
                VALUES (@rid, @title, @price, @app, @uid, @at);
                """;
            cmd.Parameters.AddWithValue("@rid", reqId);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@price", (double)price);
            cmd.Parameters.AddWithValue("@app", approved ? 1 : 0);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@at", now.AddDays(-2).ToString("O"));
            cmd.ExecuteNonQuery();
        }

        void AddReqPart(long reqId, long partId, string name, int qty, decimal price, long userId)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO request_parts (request_id, part_id, part_name, quantity, unit_price, created_by_id, created_at)
                VALUES (@rid, @pid, @pname, @qty, @price, @uid, @at);
                """;
            cmd.Parameters.AddWithValue("@rid", reqId);
            cmd.Parameters.AddWithValue("@pid", partId);
            cmd.Parameters.AddWithValue("@pname", name);
            cmd.Parameters.AddWithValue("@qty", qty);
            cmd.Parameters.AddWithValue("@price", (double)price);
            cmd.Parameters.AddWithValue("@uid", userId);
            cmd.Parameters.AddWithValue("@at", now.AddDays(-2).ToString("O"));
            cmd.ExecuteNonQuery();
        }

        var eq1 = InsertEquipment(client1, "Ноутбук", "Lenovo", "ThinkPad T14", "SN-LNV-88412");
        var eq2 = InsertEquipment(client1, "Смартфон", "Samsung", "Galaxy A54", "SN-SMG-39102");
        var eq3 = InsertEquipment(client2, "Ноутбук", "ASUS", "ROG Strix G15", "SN-ASU-55210");
        var eq4 = InsertEquipment(client2, "Монітор", "Dell", "U2722D", "SN-DEL-77193");
        var eq5 = InsertEquipment(client1, "ПК", "Custom", "Workstation i7", "SN-PC-10445");
        var eq6 = InsertEquipment(client2, "Планшет", "Apple", "iPad Air 5", "SN-APL-66201");

        // 1. Нова заявка (ще без виконавця)
        var r1 = InsertRequest("SR-2026-0001", client1, eq1, null,
            "Ноутбук сильно гріється і вимикається під навантаженням", "", "", RequestStatuses.New, 0m, 1);
        AddHistory(r1, null, RequestStatuses.New, client1, "Заявку зареєстровано клієнтом", 24);

        // 2. Призначено виконавця
        var r2 = InsertRequest("SR-2026-0002", client2, eq3, tech1,
            "Шумить правий вентилятор охолодження", "", "", RequestStatuses.Assigned, 0m, 2);
        AddHistory(r2, null, RequestStatuses.New, client2, "Заявку створено", 48);
        AddHistory(r2, RequestStatuses.New, RequestStatuses.Assigned, receiver, "Призначено виконавця Мельник А.П.", 46);

        // 3. Очікує погодження клієнтом (діагностика + роботи + деталі)
        var r3 = InsertRequest("SR-2026-0003", client1, eq2, tech2,
            "Тріснуло захисне скло та не працює сенсор у нижній частині екрана",
            "Діагностика: пошкоджено матрицю дисплейного модуля, контролер живлення справний.",
            "", RequestStatuses.AwaitingApproval, 3200m, 3);
        AddWork(r3, "Заміна дисплейного модуля", 800m, false, tech2);
        AddReqPart(r3, partDisplay, "Дисплейний модуль IPS 6.5\"", 1, 2400m, tech2);
        AddHistory(r3, null, RequestStatuses.New, client1, "Заявку створено", 72);
        AddHistory(r3, RequestStatuses.New, RequestStatuses.Assigned, receiver, "Призначено виконавця", 70);
        AddHistory(r3, RequestStatuses.Assigned, RequestStatuses.Diagnosing, tech2, "Розпочато діагностику", 65);
        AddHistory(r3, RequestStatuses.Diagnosing, RequestStatuses.AwaitingApproval, tech2, "Кошторис передано на погодження клієнту", 60);

        // 4. Погоджено клієнтом
        var r4 = InsertRequest("SR-2026-0004", client1, eq5, tech1,
            "Не визначається системний накопичувач при запуску",
            "Діагностика: збій контролера старого SSD, потрібна заміна на новий NVMe 512 ГБ.",
            "", RequestStatuses.Approved, 2250m, 4);
        AddWork(r4, "Заміна накопичувача та встановлення ОС", 600m, true, tech1);
        AddReqPart(r4, partSsd, "SSD NVMe 512 ГБ Kingston", 1, 1650m, tech1);
        AddHistory(r4, null, RequestStatuses.New, client1, "Заявку створено", 96);
        AddHistory(r4, RequestStatuses.New, RequestStatuses.AwaitingApproval, tech1, "Надіслано кошторис", 80);
        AddHistory(r4, RequestStatuses.AwaitingApproval, RequestStatuses.Approved, client1, "Клієнт погодив вартість", 75);

        // 5. У роботі
        var r5 = InsertRequest("SR-2026-0005", client2, eq6, tech2,
            "Розхитаний роз'єм USB-C, заряджається лише під кутом",
            "Діагностика: знос контактної групи роз'єму живлення.",
            "", RequestStatuses.InProgress, 1120m, 5);
        AddWork(r5, "Пайка нового роз'єму живлення USB-C", 800m, true, tech2);
        AddReqPart(r5, partPort, "Роз'єм живлення USB-C", 1, 320m, tech2);
        AddHistory(r5, null, RequestStatuses.New, client2, "Заявку створено", 120);
        AddHistory(r5, RequestStatuses.Approved, RequestStatuses.InProgress, tech2, "Роботу розпочато", 30);

        // 6. Завершена заявка
        var r6 = InsertRequest("SR-2026-0006", client1, eq1, tech1,
            "Профілактична чистка системи охолодження та заміна термопасти",
            "Діагностика: висохла термопаста, забруднений радіатор.",
            "Виконано чистку системи охолодження, замінено термопасту MX-4, стрес-тест пройдено (макс. 74°C).",
            RequestStatuses.Completed, 780m, 7);
        AddWork(r6, "Чистка системи охолодження", 500m, true, tech1);
        AddReqPart(r6, partThermal, "Термопаста Arctic MX-4 (4 г)", 1, 280m, tech1);
        AddHistory(r6, null, RequestStatuses.New, client1, "Заявку створено", 168);
        AddHistory(r6, RequestStatuses.WorkDone, RequestStatuses.Completed, receiver, "Заявку закрито та видано клієнту", 10);

        // 7-10. Додаткові заявки для демонстрації фільтрації та пагінації
        var r7 = InsertRequest("SR-2026-0007", client2, eq4, tech1,
            "Мерехтіння підсвітки монітора при яскравості нижче 50%",
            "Огляд блоку живлення та шлейфу матриці.", "", RequestStatuses.Diagnosing, 0m, 2);
        AddHistory(r7, null, RequestStatuses.New, client2, "Заявку створено", 40);
        AddHistory(r7, RequestStatuses.Assigned, RequestStatuses.Diagnosing, tech1, "Взято в діагностику", 20);

        var r8 = InsertRequest("SR-2026-0008", client2, eq3, tech1,
            "Швидко розряджається батарея ноутбука (менше 30 хв)",
            "Знос акумулятора 78%. Рекомендовано заміну.",
            "Акумулятор замінено, проведено калібрування контролера живлення.",
            RequestStatuses.WorkDone, 2400m, 3);
        AddWork(r8, "Заміна акумуляторної батареї", 500m, true, tech1);
        AddReqPart(r8, partBattery, "Акумулятор для ноутбука 4500 mAh", 1, 1900m, tech1);
        AddHistory(r8, RequestStatuses.InProgress, RequestStatuses.WorkDone, tech1, "Роботу виконано, очікує закриття приймальником", 5);

        var r9 = InsertRequest("SR-2026-0009", client1, eq2, null,
            "Періодично зникає мобільна мережа після падіння", "", "", RequestStatuses.New, 0m, 1);
        AddHistory(r9, null, RequestStatuses.New, client1, "Заявку створено", 12);

        var r10 = InsertRequest("SR-2026-0010", client2, eq4, tech2,
            "Не працює порт DisplayPort",
            "Механічне пошкодження порту на платі скалера.",
            "", RequestStatuses.Rejected, 0m, 6);
        AddHistory(r10, null, RequestStatuses.New, client2, "Заявку створено", 140);
        AddHistory(r10, RequestStatuses.AwaitingApproval, RequestStatuses.Rejected, client2, "Клієнт відмовився від ремонту через вартість заміни плати", 100);

        tx.Commit();
    }
}
