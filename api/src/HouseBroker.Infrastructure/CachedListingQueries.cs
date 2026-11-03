using HouseBroker.Application;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain;
using Microsoft.Extensions.Configuration;

namespace HouseBroker.Infrastructure;

/// <summary>Caching decorator for listing queries.</summary>
public sealed class CachedListingQueries(IListingQueries inner, ICacheService cache, IConfiguration configuration) : IListingQueries
{
    private readonly IListingQueries _inner = inner;
    private readonly ICacheService _cache = cache;
    private readonly IConfiguration _configuration = configuration;

    private bool CacheEnabled => _configuration.GetValue("ListingCache", true);

    /// <inheritdoc />
    public async Task<Listing?> GetPublicAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CacheEnabled)
        {
            return await _inner.GetPublicAsync(id, cancellationToken);
        }

        var token = await _cache.GetOrCreateAsync(MemoryCacheService.ListingCacheTokenKey, _ => Task.FromResult(Guid.NewGuid().ToString("N")), null, cancellationToken);
        var key = $"listing:{token}:public:{id}";
        return await _cache.GetOrCreateAsync(key, async ct => await _inner.GetPublicAsync(id, ct), TimeSpan.FromMinutes(5), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Listing>> GetMineAsync(Guid brokerId, CancellationToken cancellationToken)
    {
        if (!CacheEnabled)
        {
            return await _inner.GetMineAsync(brokerId, cancellationToken);
        }

        var token = await _cache.GetOrCreateAsync(MemoryCacheService.ListingCacheTokenKey, _ => Task.FromResult(Guid.NewGuid().ToString("N")), null, cancellationToken);
        var key = $"listing:{token}:mine:{brokerId}";
        return await _cache.GetOrCreateAsync(key, async ct => await _inner.GetMineAsync(brokerId, ct), TimeSpan.FromMinutes(5), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ListingSummaryDto>> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken)
    {
        if (!CacheEnabled)
        {
            return await _inner.SearchAsync(query, cancellationToken);
        }

        var token = await _cache.GetOrCreateAsync(MemoryCacheService.ListingCacheTokenKey, _ => Task.FromResult(Guid.NewGuid().ToString("N")), null, cancellationToken);
        var key = $"listing:{token}:search:{query.Location}:{query.MinPrice}:{query.MaxPrice}:{query.PropertyTypeId}:{query.Bedrooms}:{string.Join(",", query.FeatureIds)}:{query.Page}:{query.PageSize}:{query.Sort}";
        return await _cache.GetOrCreateAsync(key, async ct => await _inner.SearchAsync(query, ct), TimeSpan.FromSeconds(60), cancellationToken);
    }

    /// <inheritdoc />
    public Task<HouseBroker.Domain.BrokerProfile?> GetBrokerProfileAsync(Guid brokerId, CancellationToken cancellationToken)
        => _inner.GetBrokerProfileAsync(brokerId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<HouseBroker.Domain.PropertyType>> GetPropertyTypesAsync(CancellationToken cancellationToken)
        => _inner.GetPropertyTypesAsync(cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<HouseBroker.Domain.Feature>> GetFeaturesAsync(CancellationToken cancellationToken)
        => _inner.GetFeaturesAsync(cancellationToken);
}
