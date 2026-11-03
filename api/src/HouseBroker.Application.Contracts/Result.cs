namespace HouseBroker.Application.Contracts;

/// <summary>Result of an expected outcome.</summary>
/// <typeparam name="T">The success value type.</typeparam>
public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    /// <summary>Gets whether the result succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the success value.</summary>
    public T? Value { get; }

    /// <summary>Gets the failure details.</summary>
    public Error? Error { get; }

    /// <summary>Creates a successful result.</summary>
    public static Result<T> Success(T value) => new(true, value, null);

    /// <summary>Creates a failed result.</summary>
    public static Result<T> Failure(Error error) => new(false, default, error);
}
