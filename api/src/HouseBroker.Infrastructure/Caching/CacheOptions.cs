namespace HouseBroker.Infrastructure.Caching;

/// <summary>Cache provider settings.</summary>
public sealed class CacheOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Cache";

    /// <summary>Gets or sets the cache provider name.</summary>
    public string Provider { get; set; } = "Memory";

    /// <summary>Gets or sets Redis settings.</summary>
    public CacheRedisOptions Redis { get; set; } = new();
}

/// <summary>Redis cache settings.</summary>
public sealed class CacheRedisOptions
{
    /// <summary>Gets or sets the Redis configuration string.</summary>
    public string Configuration { get; set; } = string.Empty;
}
