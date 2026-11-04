using HouseBroker.Application.Contracts;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace HouseBroker.Infrastructure;

/// <summary>SQL-backed image storage.</summary>
public sealed class SqlImageStorage(AppDbContext dbContext, ICurrentUserService currentUser, TimeProvider clock, ICacheService cache) : IImageStorage
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly TimeProvider _clock = clock;
    private readonly ICacheService _cache = cache;

    /// <inheritdoc />
    public async Task<Result<ListingImageDto>> UploadAsync(Guid listingId, Stream content, string fileName, string contentType, long contentLength, CancellationToken cancellationToken)
    {
        var listing = await _dbContext.Listings
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken);

        if (listing is null)
        {
            return Result.Failure<ListingImageDto>(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (!_currentUser.UserId.HasValue || listing.BrokerId != _currentUser.UserId.Value)
        {
            return Result.Failure<ListingImageDto>(new ApplicationError(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        if (listing.Images.Count >= Limits.MaxImagesPerListing)
        {
            return Result.Failure<ListingImageDto>(new ApplicationError(ErrorCodes.OutOfRange, ErrorKind.Conflict));
        }

        if (string.IsNullOrWhiteSpace(contentType) || !IsAllowedContentType(contentType))
        {
            return Result.Failure<ListingImageDto>(new ApplicationError(ErrorCodes.ValidationFailed, ErrorKind.Validation, "contentType"));
        }

        if (contentLength <= 0 || contentLength > Limits.MaxImageSizeBytes)
        {
            return Result.Failure<ListingImageDto>(new ApplicationError(ErrorCodes.OutOfRange, ErrorKind.Validation, "contentLength"));
        }

        using var memoryStream = new MemoryStream();
        await content.CopyToAsync(memoryStream, cancellationToken);
        var bytes = memoryStream.ToArray();

        if (bytes.Length == 0 || bytes.Length > Limits.MaxImageSizeBytes)
        {
            return Result.Failure<ListingImageDto>(new ApplicationError(ErrorCodes.OutOfRange, ErrorKind.Validation, "content"));
        }

        if (!HasValidImageSignature(bytes, contentType))
        {
            return Result.Failure<ListingImageDto>(new ApplicationError(ErrorCodes.ValidationFailed, ErrorKind.Validation, "content"));
        }

        var extension = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin",
        };

        var storedFileName = $"{Guid.CreateVersion7():N}{extension}";
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var image = ListingImage.Create(listing.Id, storedFileName, contentType, bytes.Length, hash, !listing.Images.Any(image => image.IsPrimary));
        var imageData = ListingImageData.Create(image.Id, bytes);

        listing.AddImage(image);
        listing.Touch(_clock.GetUtcNow().UtcDateTime, _currentUser.UserId);
        await _dbContext.ListingImages.AddAsync(image, cancellationToken);
        await _dbContext.ListingImageData.AddAsync(imageData, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await _cache.InvalidateListingsAsync(cancellationToken);

        return Result.Success<ListingImageDto>(new ListingImageDto
        {
            Id = image.Id,
            FileName = image.FileName,
            ContentType = image.ContentType,
            ContentLength = image.ContentLength,
            IsPrimary = image.IsPrimary,
            SortOrder = image.SortOrder,
        });
    }

    /// <inheritdoc />
    public async Task<Result<ImageContent>> GetAsync(Guid listingId, Guid imageId, CancellationToken cancellationToken)
    {
        var image = await _dbContext.ListingImages
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == imageId && item.ListingId == listingId, cancellationToken);

        if (image is null)
        {
            return Result.Failure<ImageContent>(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        var data = await _dbContext.ListingImageData
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ListingImageId == imageId, cancellationToken);

        if (data is null)
        {
            return Result.Failure<ImageContent>(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        return Result.Success<ImageContent>(new ImageContent
        {
            Stream = new MemoryStream(data.Content),
            ContentType = image.ContentType,
            ContentLength = image.ContentLength,
            ETag = "\"" + image.ContentHash + "\"",
        });
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid listingId, Guid imageId, CancellationToken cancellationToken)
    {
        var listing = await _dbContext.Listings
            .Include(item => item.Images)
            .FirstOrDefaultAsync(item => item.Id == listingId, cancellationToken);

        if (listing is null)
        {
            return Result.Failure(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (!_currentUser.UserId.HasValue || listing.BrokerId != _currentUser.UserId.Value)
        {
            return Result.Failure(new ApplicationError(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        var image = await _dbContext.ListingImages.FirstOrDefaultAsync(item => item.Id == imageId && item.ListingId == listingId, cancellationToken);
        if (image is null)
        {
            return Result.Failure(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var data = await _dbContext.ListingImageData.FirstOrDefaultAsync(item => item.ListingImageId == imageId, cancellationToken);
        if (data is not null)
        {
            _dbContext.ListingImageData.Remove(data);
        }

        _dbContext.ListingImages.Remove(image);
        listing.RemoveImage(imageId);
        listing.Touch(_clock.GetUtcNow().UtcDateTime, _currentUser.UserId);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await _cache.InvalidateListingsAsync(cancellationToken);

        return Result.Success();
    }

    private static bool IsAllowedContentType(string contentType)
        => contentType is "image/jpeg" or "image/png" or "image/webp";

    private static bool HasValidImageSignature(byte[] bytes, string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" => bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A,
            "image/webp" => bytes.Length >= 12 && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P',
            _ => false,
        };
    }
}
