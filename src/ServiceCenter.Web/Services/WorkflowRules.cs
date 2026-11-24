using ServiceCenter.Web.Models;

namespace ServiceCenter.Web.Services;

/// <summary>
/// Чисті предметні правила переходів станів та доступу до заявок сервісного центру.
/// </summary>
public static class WorkflowRules
{
    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new()
    {
        [RequestStatuses.New] = [RequestStatuses.Assigned, RequestStatuses.Rejected, RequestStatuses.Cancelled],
        [RequestStatuses.Assigned] = [RequestStatuses.Diagnosing, RequestStatuses.Assigned, RequestStatuses.Rejected, RequestStatuses.Cancelled],
        [RequestStatuses.Diagnosing] = [RequestStatuses.AwaitingApproval, RequestStatuses.Rejected],
        [RequestStatuses.AwaitingApproval] = [RequestStatuses.Approved, RequestStatuses.Rejected, RequestStatuses.Diagnosing, RequestStatuses.Cancelled],
        [RequestStatuses.Approved] = [RequestStatuses.InProgress, RequestStatuses.Cancelled],
        [RequestStatuses.InProgress] = [RequestStatuses.WorkDone],
        [RequestStatuses.WorkDone] = [RequestStatuses.Completed],
        [RequestStatuses.Completed] = [],
        [RequestStatuses.Rejected] = [],
        [RequestStatuses.Cancelled] = []
    };

    /// <summary>
    /// Перевіряє, чи має користувач право бачити конкретну заявку.
    /// </summary>
    public static bool CanView(long userId, string role, ServiceRequestRecord req) => role switch
    {
        UserRoles.Receiver => true,
        UserRoles.Client => req.ClientId == userId,
        UserRoles.Technician => req.TechnicianId == userId,
        _ => false
    };

    /// <summary>
    /// Перевіряє допустимість переходу стану, повноваження ролі та обов'язкові інваріанти.
    /// </summary>
    public static void ValidateStatusTransition(
        ServiceRequestRecord req,
        string newStatus,
        string userRole,
        long userId,
        string? completionResult,
        string? comment,
        int workItemsCount,
        int unapprovedWorkItemsCount)
    {
        if (!RequestStatuses.All.Contains(newStatus))
        {
            throw new ValidationAppException($"Невідомий статус заявки: {newStatus}.");
        }

        if (!AllowedTransitions.TryGetValue(req.Status, out var targets) || !targets.Contains(newStatus))
        {
            throw new ConflictAppException(
                $"Перехід зі стану «{req.Status}» у стан «{newStatus}» заборонено правилами життєвого циклу заявки.",
                "INVALID_TRANSITION",
                req.Version);
        }

        // Перевірка прав на конкретний перехід
        switch (newStatus)
        {
            case RequestStatuses.Assigned:
                if (userRole != UserRoles.Receiver)
                    throw new ForbiddenAppException("Призначати виконавця може лише приймальник.");
                break;

            case RequestStatuses.Diagnosing:
                if (userRole != UserRoles.Technician || req.TechnicianId != userId)
                    throw new ForbiddenAppException("Розпочати діагностику може лише призначений виконавець.");
                break;

            case RequestStatuses.AwaitingApproval:
                if (userRole != UserRoles.Receiver && (userRole != UserRoles.Technician || req.TechnicianId != userId))
                    throw new ForbiddenAppException("Надіслати кошторис на погодження може лише призначений виконавець або приймальник.");
                if (workItemsCount <= 0)
                    throw new ConflictAppException(
                        "Перед передачею на погодження потрібно додати хоча б один вид роботи.",
                        "NO_WORK_ITEMS",
                        req.Version);
                break;

            case RequestStatuses.Approved:
                if (userRole != UserRoles.Client || req.ClientId != userId)
                    throw new ForbiddenAppException("Погодити перелік робіт може лише клієнт-власник заявки.");
                break;

            case RequestStatuses.InProgress:
                if (userRole != UserRoles.Technician || req.TechnicianId != userId)
                    throw new ForbiddenAppException("Розпочати виконання робіт може лише призначений виконавець.");
                if (unapprovedWorkItemsCount > 0 || workItemsCount <= 0)
                    throw new ConflictAppException(
                        "Неможливо розпочати роботу без погодженого клієнтом переліку робіт.",
                        "WORK_NOT_APPROVED",
                        req.Version);
                break;

            case RequestStatuses.WorkDone:
                if (userRole != UserRoles.Technician || req.TechnicianId != userId)
                    throw new ForbiddenAppException("Зафіксувати виконання може лише призначений виконавець.");
                if (unapprovedWorkItemsCount > 0 || workItemsCount <= 0)
                    throw new ConflictAppException(
                        "Заборонено завершувати непогоджену роботу.",
                        "WORK_NOT_APPROVED",
                        req.Version);
                if (string.IsNullOrWhiteSpace(completionResult) && string.IsNullOrWhiteSpace(req.CompletionResult))
                    throw new ValidationAppException(
                        "Для переходу у стан «Роботу виконано» необхідно вказати результат виконання.",
                        new Dictionary<string, string[]> { ["completionResult"] = ["Поле результату виконання є обов'язковим."] });
                break;

            case RequestStatuses.Completed:
                if (userRole != UserRoles.Receiver)
                    throw new ForbiddenAppException("Остаточно завершити та закрити заявку може лише приймальник.");
                if (unapprovedWorkItemsCount > 0 || workItemsCount <= 0)
                    throw new ConflictAppException(
                        "Заборонено закривати заявку з непогодженим переліком робіт.",
                        "WORK_NOT_APPROVED",
                        req.Version);
                if (string.IsNullOrWhiteSpace(completionResult) && string.IsNullOrWhiteSpace(req.CompletionResult))
                    throw new ValidationAppException(
                        "Заборонено завершувати заявку без результату виконання.",
                        new Dictionary<string, string[]> { ["completionResult"] = ["Вкажіть результат виконання перед закриттям."] });
                break;

            case RequestStatuses.Rejected:
                var isOwnerClient = userRole == UserRoles.Client && req.ClientId == userId;
                if (userRole != UserRoles.Receiver && !isOwnerClient)
                    throw new ForbiddenAppException("Відхилити заявку може лише клієнт-власник (на етапі погодження) або приймальник.");
                if (isOwnerClient && req.Status != RequestStatuses.AwaitingApproval)
                    throw new ForbiddenAppException("Клієнт може відхилити лише кошторис, що очікує погодження.");
                if (string.IsNullOrWhiteSpace(comment))
                    throw new ValidationAppException(
                        "При відхиленні заявки необхідно вказати причину.",
                        new Dictionary<string, string[]> { ["comment"] = ["Причина відхилення є обов'язковою."] });
                break;

            case RequestStatuses.Cancelled:
                var isClientOwner = userRole == UserRoles.Client && req.ClientId == userId;
                if (userRole != UserRoles.Receiver && !isClientOwner)
                    throw new ForbiddenAppException("Скасувати заявку може лише її власник або приймальник.");
                break;
        }
    }

    public static bool CanEditDiagnosticsOrEstimate(ServiceRequestRecord req, string role, long userId)
    {
        if (role == UserRoles.Technician && req.TechnicianId == userId)
        {
            return req.Status is RequestStatuses.Assigned or RequestStatuses.Diagnosing or RequestStatuses.AwaitingApproval;
        }
        if (role == UserRoles.Receiver)
        {
            return req.Status is RequestStatuses.New or RequestStatuses.Assigned or RequestStatuses.Diagnosing or RequestStatuses.AwaitingApproval;
        }
        return false;
    }
}
