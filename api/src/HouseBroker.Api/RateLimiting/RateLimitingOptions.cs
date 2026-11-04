namespace HouseBroker.Api.RateLimiting;

/// <summary>Configuration-backed rate limit policy settings.</summary>
public sealed class RateLimitingOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>Gets or sets the global policy applied to every non-health endpoint.</summary>
    public RateLimitPolicyOptions Global { get; set; } = new() { PermitLimit = 100 };

    /// <summary>Gets or sets the search policy applied to listing search and lookups.</summary>
    public RateLimitPolicyOptions Search { get; set; } = new() { PermitLimit = 60 };

    /// <summary>Gets or sets the write policy applied to listing and commission mutations.</summary>
    public RateLimitPolicyOptions Write { get; set; } = new() { PermitLimit = 30 };

    /// <summary>Gets or sets the upload policy applied to image uploads.</summary>
    public RateLimitPolicyOptions Upload { get; set; } = new() { PermitLimit = 10 };
}

/// <summary>Settings for a single rate limit policy.</summary>
public sealed class RateLimitPolicyOptions
{
    /// <summary>Gets or sets the number of requests permitted per window.</summary>
    public int PermitLimit { get; set; } = 60;

    /// <summary>Gets or sets the sliding window length in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;
}
