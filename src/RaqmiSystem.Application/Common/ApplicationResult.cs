namespace RaqmiSystem.Application.Common;

public sealed class ApplicationResult<T>
{
    private ApplicationResult(T? value, ApplicationErrorType errorType, string? error)
    {
        Value = value;
        ErrorType = errorType;
        Error = error;
    }

    public bool Succeeded => ErrorType == ApplicationErrorType.None;

    public T? Value { get; }

    public ApplicationErrorType ErrorType { get; }

    public string? Error { get; }

    public static ApplicationResult<T> Success(T value)
    {
        return new ApplicationResult<T>(value, ApplicationErrorType.None, null);
    }

    public static ApplicationResult<T> NotFound(string message)
    {
        return new ApplicationResult<T>(default, ApplicationErrorType.NotFound, message);
    }

    public static ApplicationResult<T> Conflict(string message)
    {
        return new ApplicationResult<T>(default, ApplicationErrorType.Conflict, message);
    }

    public static ApplicationResult<T> Validation(string message)
    {
        return new ApplicationResult<T>(default, ApplicationErrorType.Validation, message);
    }
}
