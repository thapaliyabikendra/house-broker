namespace HouseBroker.Application.Contracts;

/// <summary>Non-generic result.</summary>
public sealed class Result
{
    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Gets whether the result succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Gets the failure details.</summary>
    public Error? Error { get; }

    /// <summary>Creates a successful result.</summary>
    public static Result Success() => new(true, null);

    /// <summary>Creates a failed result.</summary>
    public static Result Failure(Error error) => new(false, error);
}
