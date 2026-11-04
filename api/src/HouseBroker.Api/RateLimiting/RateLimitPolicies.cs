namespace HouseBroker.Api.RateLimiting;

/// <summary>Stable rate limit policy names.</summary>
public static class RateLimitPolicies
{
    /// <summary>Policy for listing search and reference lookups.</summary>
    public const string Search = "search";

    /// <summary>Policy for listing and commission mutations.</summary>
    public const string Write = "write";

    /// <summary>Policy for image uploads.</summary>
    public const string Upload = "upload";

    internal const string Global = "global";
}
