namespace HouseBroker.Application.Contracts;

/// <summary>Non-generic result.</summary>
public sealed class Result
{
    private Result(bool isSuccess, ApplicationError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Gets whether the result succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the failure details.</summary>
    public ApplicationError? Error { get; }

    /// <summary>Creates a successful non-generic result.</summary>
    public static Result Success() => new(true, null);

    /// <summary>Creates a failed non-generic result.</summary>
    public static Result Failure(ApplicationError error) => new(false, error);

    /// <summary>Creates a successful generic result.</summary>
    public static Result<T> Success<T>(T value) => new Result<T>(true, value, null);

    /// <summary>Creates a failed generic result.</summary>
    public static Result<T> Failure<T>(ApplicationError error) => new Result<T>(false, default, error);
}
