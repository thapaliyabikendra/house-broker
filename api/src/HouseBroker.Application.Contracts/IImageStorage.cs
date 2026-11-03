namespace HouseBroker.Application.Contracts;

/// <summary>Image storage abstraction.</summary>
public interface IImageStorage
{
    /// <summary>Uploads an image for a listing.</summary>
    Task<Result<ListingImageDto>> UploadAsync(Guid listingId, Stream content, string fileName, string contentType, long contentLength, CancellationToken cancellationToken);

    /// <summary>Deletes an image from a listing.</summary>
    Task<Result> DeleteAsync(Guid listingId, Guid imageId, CancellationToken cancellationToken);

    /// <summary>Gets image content.</summary>
    Task<Result<ImageContent>> GetAsync(Guid listingId, Guid imageId, CancellationToken cancellationToken);
}

/// <summary>Image content.</summary>
public sealed class ImageContent
{
    /// <summary>Gets or sets the content stream.</summary>
    public Stream Stream { get; set; } = Stream.Null;

    /// <summary>Gets or sets the content type.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Gets or sets the content length.</summary>
    public long ContentLength { get; set; }

    /// <summary>Gets or sets the ETag.</summary>
    public string? ETag { get; set; }
}
