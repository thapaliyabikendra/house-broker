using System.Globalization;
using System.Text;
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

    /// <summary>The normalized culture used in cache keys.</summary>
    private static string Culture
    {
        get
        {
            var name = CultureInfo.CurrentUICulture.Name;
            return string.IsNullOrWhiteSpace(name) ? "en" : name.Trim();
        }
    }

    /// <inheritdoc />
    public async Task<Listing?> GetPublicAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!CacheEnabled)
        {
            return await _inner.GetPublicAsync(id, cancellationToken);
        }

        var token = await _cache.GetOrCreateAsync(MemoryCacheService.ListingCacheTokenKey, _ => Task.FromResult(Guid.NewGuid().ToString("N")), null, cancellationToken);
        var key = $"listing:{token}:public:{id}:{Culture}";
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
        var key = $"listing:{token}:mine:{brokerId}:{Culture}";
        return await _cache.GetOrCreateAsync(key, async ct => await _inner.GetMineAsync(brokerId, ct), TimeSpan.FromMinutes(5), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ListingSummaryDto>> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken)
    {
        query ??= new ListingSearchQuery();
        if (!CacheEnabled)
        {
            return await _inner.SearchAsync(query, cancellationToken);
        }

        var token = await _cache.GetOrCreateAsync(MemoryCacheService.ListingCacheTokenKey, _ => Task.FromResult(Guid.NewGuid().ToString("N")), null, cancellationToken);
        var key = BuildSearchKey(token, query);
        return await _cache.GetOrCreateAsync(key, async ct => await _inner.SearchAsync(query, ct), TimeSpan.FromSeconds(60), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HouseBroker.Domain.BrokerProfile?> GetBrokerProfileAsync(Guid brokerId, CancellationToken cancellationToken)
    {
        if (!CacheEnabled)
        {
            return await _inner.GetBrokerProfileAsync(brokerId, cancellationToken);
        }

        var token = await _cache.GetOrCreateAsync(MemoryCacheService.ListingCacheTokenKey, _ => Task.FromResult(Guid.NewGuid().ToString("N")), null, cancellationToken);
        var key = $"listing:{token}:broker-profile:{brokerId}:{Culture}";
        return await _cache.GetOrCreateAsync(key, async ct => await _inner.GetBrokerProfileAsync(brokerId, ct), TimeSpan.FromMinutes(5), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HouseBroker.Domain.PropertyType>> GetPropertyTypesAsync(CancellationToken cancellationToken)
    {
        if (!CacheEnabled)
        {
            return await _inner.GetPropertyTypesAsync(cancellationToken);
        }

        var token = await _cache.GetOrCreateAsync(MemoryCacheService.ListingCacheTokenKey, _ => Task.FromResult(Guid.NewGuid().ToString("N")), null, cancellationToken);
        var key = $"listing:{token}:lookup:property-types:{Culture}";
        return await _cache.GetOrCreateAsync(key, async ct => await _inner.GetPropertyTypesAsync(ct), TimeSpan.FromMinutes(30), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HouseBroker.Domain.Feature>> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        if (!CacheEnabled)
        {
            return await _inner.GetFeaturesAsync(cancellationToken);
        }

        var token = await _cache.GetOrCreateAsync(MemoryCacheService.ListingCacheTokenKey, _ => Task.FromResult(Guid.NewGuid().ToString("N")), null, cancellationToken);
        var key = $"listing:{token}:lookup:features:{Culture}";
        return await _cache.GetOrCreateAsync(key, async ct => await _inner.GetFeaturesAsync(ct), TimeSpan.FromMinutes(30), cancellationToken);
    }

    /// <inheritdoc />
    public Task UpsertBrokerProfileAsync(HouseBroker.Domain.BrokerProfile profile, CancellationToken cancellationToken)
        => _inner.UpsertBrokerProfileAsync(profile, cancellationToken);

    /// <summary>Builds a normalized, culture-aware cache key for a search query.</summary>
    public static string BuildSearchKey(string token, ListingSearchQuery query)
    {
        query ??= new ListingSearchQuery();
        var location = (query.Location ?? string.Empty).Trim().Normalize(NormalizationForm.FormC);
        if (!string.IsNullOrWhiteSpace(location))
        {
            location = location.ToLowerInvariant();
        }

        var featureIds = string.Join(",", query.FeatureIds.Distinct().OrderBy(id => id));
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var sort = (query.Sort ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(sort))
        {
            sort = "default";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"listing:{token}:search:culture={Culture}:location={location}:min={query.MinPrice?.ToString("G", CultureInfo.InvariantCulture)}:max={query.MaxPrice?.ToString("G", CultureInfo.InvariantCulture)}:propertyType={query.PropertyTypeId?.ToString("D")}:bedrooms={query.Bedrooms?.ToString(CultureInfo.InvariantCulture)}:features={featureIds}:page={page}:pageSize={pageSize}:sort={sort}");
    }
}
