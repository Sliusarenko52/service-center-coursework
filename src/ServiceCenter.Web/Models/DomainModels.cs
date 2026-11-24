namespace ServiceCenter.Web.Models;

/// <summary>
/// Ролі користувачів у системі заявок сервісного центру.
/// </summary>
public static class UserRoles
{
    public const string Client = "CLIENT";
    public const string Receiver = "RECEIVER";
    public const string Technician = "TECHNICIAN";

    public static readonly string[] All = [Client, Receiver, Technician];
}

/// <summary>
/// Допустимі стани заявки на обслуговування та ремонт обладнання.
/// </summary>
public static class RequestStatuses
{
    public const string New = "NEW";
    public const string Assigned = "ASSIGNED";
    public const string Diagnosing = "DIAGNOSING";
    public const string AwaitingApproval = "AWAITING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string InProgress = "IN_PROGRESS";
    public const string WorkDone = "WORK_DONE";
    public const string Completed = "COMPLETED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";

    public static readonly string[] All =
    [
        New, Assigned, Diagnosing, AwaitingApproval,
        Approved, InProgress, WorkDone, Completed, Rejected, Cancelled
    ];
}

/// <summary>
/// Користувач системи (клієнт, приймальник або виконавець).
/// </summary>
public sealed class UserRecord
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.Client;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Обладнання, передане клієнтом на обслуговування.
/// </summary>
public sealed class EquipmentRecord
{
    public long Id { get; set; }
    public long ClientId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Заявка сервісного центру з контролем версії для оптимістичного блокування.
/// </summary>
public sealed class ServiceRequestRecord
{
    public long Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public long ClientId { get; set; }
    public long EquipmentId { get; set; }
    public long? TechnicianId { get; set; }
    public string IssueDescription { get; set; } = string.Empty;
    public string DiagnosticsNotes { get; set; } = string.Empty;
    public string CompletionResult { get; set; } = string.Empty;
    public string Status { get; set; } = RequestStatuses.New;
    public decimal TotalCost { get; set; }
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Вид роботи у кошторисі заявки.
/// </summary>
public sealed class WorkItemRecord
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsApproved { get; set; }
    public long CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Деталь (запчастина) на складі сервісного центру.
/// </summary>
public sealed class PartRecord
{
    public long Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int StockQty { get; set; }
}

/// <summary>
/// Використана деталь у межах конкретної заявки.
/// </summary>
public sealed class RequestPartRecord
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public long PartId { get; set; }
    public string PartName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public long CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Запис журналу переходів станів заявки.
/// </summary>
public sealed class StatusHistoryRecord
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public long ChangedById { get; set; }
    public string ChangedByName { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
