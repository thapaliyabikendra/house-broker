using HouseBroker.Application.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HouseBroker.Infrastructure;

/// <summary>In-memory cache implementation.</summary>
public sealed class MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService> logger) : ICacheService
{
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ILogger<MemoryCacheService> _logger = logger;

    /// <inheritdoc />
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out var value) && value is T typed)
            {
                return Task.FromResult<T?>(typed);
            }

            return Task.FromResult<T?>(default);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cache get failed for key {CacheKey}.", key);
            return Task.FromResult<T?>(default);
        }
    }

    /// <inheritdoc />
    public Task SetAsync<T>(string key, T value, TimeSpan? expiration, CancellationToken cancellationToken)
    {
        try
        {
            var options = new MemoryCacheEntryOptions();
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration.Value;
            }

            _memoryCache.Set(key, value, options);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cache set failed for key {CacheKey}.", key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            _memoryCache.Remove(key);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cache remove failed for key {CacheKey}.", key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration, CancellationToken cancellationToken)
    {
        try
        {
            if (_memoryCache.TryGetValue(key, out var cached) && cached is T typed)
            {
                return typed;
            }

            var value = await factory(cancellationToken);
            await SetAsync(key, value, expiration, cancellationToken);
            return value;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cache get-or-create failed for key {CacheKey}.", key);
            return await factory(cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task InvalidateListingsAsync(CancellationToken cancellationToken)
    {
        // Memory provider uses a version token key consumed by CachedListingQueries.
        return SetAsync(ListingCacheTokenKey, Guid.NewGuid().ToString("N"), null, cancellationToken);
    }

    /// <inheritdoc />
    public Task InvalidateCommissionRatesAsync(CancellationToken cancellationToken)
    {
        return SetAsync(CommissionRateCacheTokenKey, Guid.NewGuid().ToString("N"), null, cancellationToken);
    }

    internal const string ListingCacheTokenKey = "cache:token:listings";
    internal const string CommissionRateCacheTokenKey = "cache:token:commission-rates";
}
