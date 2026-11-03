namespace HouseBroker.Domain;

/// <summary>Listing image metadata.</summary>
public sealed class ListingImage
{
    private ListingImage()
    {
    }

    /// <summary>Initializes an image.</summary>
    public static ListingImage Create(Guid listingId, string fileName, string contentType, long contentLength, string contentHash, bool isPrimary)
        => new()
        {
            Id = Guid.CreateVersion7(),
            ListingId = listingId,
            FileName = fileName,
            ContentType = contentType,
            ContentLength = contentLength,
            ContentHash = contentHash,
            SortOrder = 0,
            IsPrimary = isPrimary,
        };

    /// <summary>Gets the image identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the listing identifier.</summary>
    public Guid ListingId { get; private set; }

    /// <summary>Gets the file name.</summary>
    public string FileName { get; private set; } = string.Empty;

    /// <summary>Gets the content type.</summary>
    public string ContentType { get; private set; } = string.Empty;

    /// <summary>Gets the content length.</summary>
    public long ContentLength { get; private set; }

    /// <summary>Gets the content hash.</summary>
    public string ContentHash { get; private set; } = string.Empty;

    /// <summary>Gets the sort order.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Gets whether this is the primary image.</summary>
    public bool IsPrimary { get; private set; }

    /// <summary>Gets the created timestamp.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Gets the creator.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>Gets the updated timestamp.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Gets the updater.</summary>
    public Guid? UpdatedBy { get; private set; }
}

/// <summary>Binary content for a listing image.</summary>
public sealed class ListingImageData
{
    private ListingImageData()
    {
    }

    /// <summary>Initializes image data.</summary>
    public static ListingImageData Create(Guid listingImageId, byte[] content)
        => new()
        {
            ListingImageId = listingImageId,
            Content = content,
        };

    /// <summary>Gets the image identifier.</summary>
    public Guid ListingImageId { get; private set; }

    /// <summary>Gets the binary content.</summary>
    public byte[] Content { get; private set; } = Array.Empty<byte>();
}

/// <summary>Join between listings and features.</summary>
public sealed class ListingFeature
{
    private ListingFeature()
    {
    }

    /// <summary>Initializes a listing feature.</summary>
    public static ListingFeature Create(Guid listingId, Guid featureId)
        => new()
        {
            Id = Guid.CreateVersion7(),
            ListingId = listingId,
            FeatureId = featureId,
        };

    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the listing identifier.</summary>
    public Guid ListingId { get; private set; }

    /// <summary>Gets the feature identifier.</summary>
    public Guid FeatureId { get; private set; }

    /// <summary>Gets the created timestamp.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Gets the creator.</summary>
    public Guid? CreatedBy { get; private set; }
}
