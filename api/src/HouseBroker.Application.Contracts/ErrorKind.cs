namespace HouseBroker.Application.Contracts;

/// <summary>Kinds of expected failures.</summary>
public enum ErrorKind
{
    /// <summary>Validation failure.</summary>
    Validation,

    /// <summary>Resource not found.</summary>
    NotFound,

    /// <summary>Access forbidden.</summary>
    Forbidden,

    /// <summary>Conflict with current state.</summary>
    Conflict,

    /// <summary>Precondition failed.</summary>
    PreconditionFailed,

    /// <summary>A required precondition is missing.</summary>
    PreconditionRequired,

    /// <summary>Rate limit exceeded.</summary>
    RateLimited,
}
