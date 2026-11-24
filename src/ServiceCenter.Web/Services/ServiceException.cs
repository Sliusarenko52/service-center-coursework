namespace ServiceCenter.Web.Services;

public abstract class AppException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }
    public IDictionary<string, string[]>? FieldErrors { get; }

    protected AppException(
        int statusCode,
        string errorCode,
        string message,
        IDictionary<string, string[]>? fieldErrors = null) : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        FieldErrors = fieldErrors;
    }
}

public sealed class ValidationAppException : AppException
{
    public ValidationAppException(string message, IDictionary<string, string[]>? fieldErrors = null)
        : base(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", message, fieldErrors) { }
}

public sealed class ForbiddenAppException : AppException
{
    public ForbiddenAppException(string message = "Доступ до цієї операції або запису заборонено.")
        : base(StatusCodes.Status403Forbidden, "FORBIDDEN", message) { }
}

public sealed class NotFoundAppException : AppException
{
    public NotFoundAppException(string message = "Запитаний запис не знайдено.")
        : base(StatusCodes.Status404NotFound, "NOT_FOUND", message) { }
}

public sealed class ConflictAppException : AppException
{
    public int? CurrentVersion { get; }

    public ConflictAppException(string message, string code = "CONFLICT", int? currentVersion = null)
        : base(StatusCodes.Status409Conflict, code, message)
    {
        CurrentVersion = currentVersion;
    }
}
