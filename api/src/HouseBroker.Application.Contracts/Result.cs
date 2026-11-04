namespace HouseBroker.Application.Contracts;

/// <summary>Result of an expected outcome.</summary>
/// <typeparam name="T">The success value type.</typeparam>
public sealed class Result<T>
{
    internal Result(bool isSuccess, T? value, ApplicationError? error)
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
    public ApplicationError? Error { get; }
}
