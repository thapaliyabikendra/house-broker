using HouseBroker.Application.Contracts;
using HouseBroker.Domain;

namespace HouseBroker.Application;

/// <summary>Commission rate provider.</summary>
public interface ICommissionRateProvider
{
    /// <summary>Gets active rates at the supplied instant.</summary>
    Task<IReadOnlyList<CommissionRate>> GetActiveAsync(DateTime effectiveAtUtc, CancellationToken cancellationToken);
}

/// <summary>Listing repository.</summary>
public interface IListingRepository
{
    /// <summary>Gets a listing.</summary>
    Task<Domain.Listing?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Adds a listing.</summary>
    Task AddAsync(Domain.Listing listing, CancellationToken cancellationToken);

    /// <summary>Removes a listing.</summary>
    Task RemoveAsync(Domain.Listing listing, CancellationToken cancellationToken);
}

/// <summary>Listing queries.</summary>
public interface IListingQueries
{
    /// <summary>Gets a listing for public view.</summary>
    Task<Domain.Listing?> GetPublicAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Gets listings for an owner.</summary>
    Task<IReadOnlyList<Domain.Listing>> GetMineAsync(Guid brokerId, CancellationToken cancellationToken);

    /// <summary>Searches listings.</summary>
    Task<PagedResult<ListingSummaryDto>> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken);
}