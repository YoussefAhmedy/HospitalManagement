namespace Hospital.BLL.Common;

/// <summary>
/// Explicit success/failure result for service operations that can legitimately fail for
/// business reasons. Avoids boolean-return services that swallow the reason.
/// </summary>
public sealed class Result
{
    private Result(bool succeeded, string? error, string? code)
    {
        Succeeded = succeeded;
        Error = error;
        Code = code;
    }

    public bool Succeeded { get; }
    public string? Error { get; }
    public string? Code { get; }

    public static Result Ok() => new(true, null, null);
    public static Result Fail(string error, string? code = null) => new(false, error, code);
}

public sealed class Result<T>
{
    private Result(T? value, bool succeeded, string? error, string? code)
    {
        Value = value;
        Succeeded = succeeded;
        Error = error;
        Code = code;
    }

    public T? Value { get; }
    public bool Succeeded { get; }
    public string? Error { get; }
    public string? Code { get; }

    public static Result<T> Ok(T value) => new(value, true, null, null);
    public static Result<T> Fail(string error, string? code = null) => new(default, false, error, code);
}
