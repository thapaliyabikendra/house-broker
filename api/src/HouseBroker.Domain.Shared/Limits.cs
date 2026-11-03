namespace HouseBroker.Domain.Shared;

/// <summary>Shared operational limits.</summary>
public static class Limits
{
    /// <summary>Maximum images per listing.</summary>
    public const int MaxImagesPerListing = 10;

    /// <summary>Maximum size of an uploaded image in bytes (5 MB).</summary>
    public const int MaxImageSizeBytes = 5 * 1024 * 1024;

    /// <summary>Maximum page size for search results.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Default page size for search results.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Maximum accepted text search length.</summary>
    public const int MaxSearchTextLength = 100;

    /// <summary>Maximum listing title length.</summary>
    public const int MaxTitleLength = 200;

    /// <summary>Minimum allowed listing price.</summary>
    public const decimal MinPrice = 0m;
}
