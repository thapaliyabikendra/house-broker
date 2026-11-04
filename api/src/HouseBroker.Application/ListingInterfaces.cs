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

    /// <summary>Adds a price history entry.</summary>
    Task AddPriceHistoryAsync(Domain.ListingPriceHistory priceHistory, CancellationToken cancellationToken);

    /// <summary>Adds a status history entry.</summary>
    Task AddStatusHistoryAsync(Domain.ListingStatusHistory statusHistory, CancellationToken cancellationToken);
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

    /// <summary>Gets a broker profile.</summary>
    Task<BrokerProfile?> GetBrokerProfileAsync(Guid brokerId, CancellationToken cancellationToken);

    /// <summary>Upserts a broker profile.</summary>
    Task UpsertBrokerProfileAsync(BrokerProfile profile, CancellationToken cancellationToken);

    /// <summary>Gets property types.</summary>
    Task<IReadOnlyList<PropertyType>> GetPropertyTypesAsync(CancellationToken cancellationToken);

    /// <summary>Gets features.</summary>
    Task<IReadOnlyList<Feature>> GetFeaturesAsync(CancellationToken cancellationToken);
}
