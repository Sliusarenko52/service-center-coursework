namespace ServiceCenter.Web.Models;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string Email, string Password, string FullName, string Phone);

public sealed record UserDto(long Id, string Email, string FullName, string Phone, string Role);

public sealed record CreateServiceRequestDto(
    long? ClientId,
    string Category,
    string Brand,
    string Model,
    string SerialNumber,
    string IssueDescription);

public sealed record AssignTechnicianDto(long TechnicianId, int Version);

public sealed record UpdateDiagnosticsDto(string DiagnosticsNotes, int Version);

public sealed record AddWorkItemDto(string Title, decimal Price, int Version);

public sealed record AddRequestPartDto(long PartId, int Quantity, int Version);

public sealed record ChangeStatusDto(string NewStatus, string? Comment, string? CompletionResult, int Version);

public sealed record WorkItemDto(
    long Id,
    string Title,
    decimal Price,
    bool IsApproved,
    DateTime CreatedAt);

public sealed record RequestPartDto(
    long Id,
    long PartId,
    string PartName,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    DateTime CreatedAt);

public sealed record StatusHistoryDto(
    long Id,
    string? FromStatus,
    string ToStatus,
    long ChangedById,
    string ChangedByName,
    string Comment,
    DateTime ChangedAt);

public sealed record ServiceRequestSummaryDto(
    long Id,
    string RequestNumber,
    long ClientId,
    string ClientName,
    string EquipmentTitle,
    string SerialNumber,
    long? TechnicianId,
    string? TechnicianName,
    string Status,
    decimal TotalCost,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ServiceRequestDetailsDto(
    long Id,
    string RequestNumber,
    long ClientId,
    string ClientName,
    string ClientPhone,
    long EquipmentId,
    string EquipmentCategory,
    string EquipmentBrand,
    string EquipmentModel,
    string SerialNumber,
    long? TechnicianId,
    string? TechnicianName,
    string IssueDescription,
    string DiagnosticsNotes,
    string CompletionResult,
    string Status,
    decimal WorksTotal,
    decimal PartsTotal,
    decimal TotalCost,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkItemDto> WorkItems,
    IReadOnlyList<RequestPartDto> Parts,
    IReadOnlyList<StatusHistoryDto> History);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
