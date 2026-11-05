namespace HouseBroker.Domain.Common;

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

    /// <summary>A free-text field contains HTML markup.</summary>
    public const string HtmlNotAllowed = "validation.html_not_allowed";

    /// <summary>A value does not match the expected format (for example the postal code).</summary>
    public const string InvalidFormat = "validation.invalid_format";

    /// <summary>Exactly one commission tier may be open-ended.</summary>
    public const string TierOpenEndedCount = "validation.tier_open_ended_count";

    /// <summary>Only the highest active commission tier may be open-ended.</summary>
    public const string TierTopOpenEnded = "validation.tier_top_open_ended";

    /// <summary>An open-ended commission tier must be the last tier in the set.</summary>
    public const string TierOpenEndedLast = "validation.tier_open_ended_last";

    /// <summary>A bounded commission tier's maximum must be greater than zero and greater than the previous band's maximum.</summary>
    public const string TierMaxNotAscending = "validation.tier_max_not_ascending";

    /// <summary>A commission tier's maximum must be greater than zero.</summary>
    public const string TierMaxNotAboveMin = "validation.tier_max_not_above_min";

    /// <summary>A commission tier's rate must be greater than zero and at most 100 percent.</summary>
    public const string TierRateOutOfRange = "validation.tier_rate_out_of_range";

    /// <summary>At least one commission tier must be active.</summary>
    public const string TierSetEmpty = "validation.tier_set_empty";
}
