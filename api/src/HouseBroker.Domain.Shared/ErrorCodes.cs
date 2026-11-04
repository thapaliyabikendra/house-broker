namespace HouseBroker.Domain.Shared;

/// <summary>Stable error codes returned by the application layer.</summary>
public static class ErrorCodes
{
    /// <summary>Generic validation failure.</summary>
    public const string ValidationFailed = "validation.failed";

    /// <summary>A required field is missing.</summary>
    public const string Required = "validation.required";

    /// <summary>A value is outside the allowed range.</summary>
    public const string OutOfRange = "validation.out_of_range";

    /// <summary>A lookup value does not exist.</summary>
    public const string NotFound = "not_found";

    /// <summary>The requested resource does not exist.</summary>
    public const string ResourceNotFound = "resource.not_found";

    /// <summary>The caller lacks permission.</summary>
    public const string Forbidden = "forbidden";

    /// <summary>The caller does not own the resource.</summary>
    public const string NotOwner = "not_owner";

    /// <summary>The operation conflicts with current state.</summary>
    public const string Conflict = "conflict";

    /// <summary>A required precondition header is missing.</summary>
    public const string PreconditionRequired = "precondition.required";

    /// <summary>A precondition failed.</summary>
    public const string PreconditionFailed = "precondition.failed";

    /// <summary>A rate limit was exceeded.</summary>
    public const string RateLimited = "rate_limited";

    /// <summary>A required feature is disabled.</summary>
    public const string FeatureDisabled = "feature.disabled";

    /// <summary>An instant without an offset was supplied.</summary>
    public const string OffsetRequired = "validation.offset_required";

    /// <summary>An idempotency key was reused with a different request.</summary>
    public const string IdempotencyConflict = "idempotency.conflict";

    /// <summary>An idempotency key is already being processed.</summary>
    public const string IdempotencyInProgress = "idempotency.in_progress";

    /// <summary>A culture is not supported.</summary>
    public const string UnsupportedCulture = "validation.unsupported_culture";
}
