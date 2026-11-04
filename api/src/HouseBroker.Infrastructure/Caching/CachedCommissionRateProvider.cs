using HouseBroker.Application.Abstractions;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain;
using Microsoft.Extensions.Configuration;

namespace HouseBroker.Infrastructure.Caching;

/// <summary>Caching decorator for commission rate provider.</summary>
public sealed class CachedCommissionRateProvider(ICommissionRateProvider inner, ICacheService cache, IConfiguration configuration) : ICommissionRateProvider
{
    private readonly ICommissionRateProvider _inner = inner;
    private readonly ICacheService _cache = cache;
    private readonly IConfiguration _configuration = configuration;

    private bool CacheEnabled => _configuration.GetValue("ListingCache", true);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommissionRate>> GetActiveAsync(DateTime effectiveAtUtc, CancellationToken cancellationToken)
    {
        if (!CacheEnabled)
        {
            return await _inner.GetActiveAsync(effectiveAtUtc, cancellationToken);
        }

        var token = await _cache.GetOrCreateAsync(MemoryCacheService.CommissionRateCacheTokenKey, _ => Task.FromResult(Guid.NewGuid().ToString("N")), TimeSpan.FromDays(30), cancellationToken);
        var key = $"commission:{token}:active:{effectiveAtUtc:O}";
        return await _cache.GetOrCreateAsync(key, async ct => await _inner.GetActiveAsync(effectiveAtUtc, ct), TimeSpan.FromMinutes(30), cancellationToken);
    }
}

