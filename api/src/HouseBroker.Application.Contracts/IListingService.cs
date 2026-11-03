
namespace HouseBroker.Application.Contracts;

/// <summary>Listing service.</summary>
public interface IListingService
{
    /// <summary>Creates a listing.</summary>
    Task<Result<OwnerListingDto>> CreateAsync(CreateListingRequest request, CancellationToken cancellationToken);

    /// <summary>Updates a listing.</summary>
    Task<Result<OwnerListingDto>> UpdateAsync(Guid id, UpdateListingRequest request, string? ifMatch, CancellationToken cancellationToken);

    /// <summary>Deletes a listing.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Gets public detail.</summary>
    Task<Result<ListingDetailDto>> GetDetailAsync(Guid id, bool includeContact, CancellationToken cancellationToken);

    /// <summary>Searches listings.</summary>
    Task<Result<PagedResult<ListingSummaryDto>>> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken);

    /// <summary>Gets owner detail.</summary>
    Task<Result<OwnerListingDto>> GetOwnerDetailAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Gets owned listings.</summary>
    Task<Result<IReadOnlyList<OwnerListingDto>>> GetMineAsync(CancellationToken cancellationToken);

    /// <summary>Publishes a listing.</summary>
    Task<Result<OwnerListingDto>> PublishAsync(Guid id, CancellationToken cancellationToken);
}
