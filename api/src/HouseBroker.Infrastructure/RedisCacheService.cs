using System.Collections.Concurrent;
using System.Text.Json;
using HouseBroker.Application.Contracts;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace HouseBroker.Infrastructure;

/// <summary>Redis-backed cache implementation.</summary>
public sealed class RedisCacheService(IDistributedCache distributedCache, ILogger<RedisCacheService> logger) : ICacheService
{
    private readonly IDistributedCache _distributedCache = distributedCache;
    private readonly ILogger<RedisCacheService> _logger = logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(bytes);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cache get failed for key {CacheKey}.", key);
            return default;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration, CancellationToken cancellationToken)
    {
        try
        {
            var options = new DistributedCacheEntryOptions();
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration.Value;
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
            await _distributedCache.SetAsync(key, bytes, options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cache set failed for key {CacheKey}.", key);
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Cache remove failed for key {CacheKey}.", key);
        }
    }

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration, CancellationToken cancellationToken)
    {
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                var bytes = await _distributedCache.GetAsync(key, cancellationToken).ConfigureAwait(false);
                if (bytes is { Length: > 0 })
                {
                    var cached = JsonSerializer.Deserialize<T>(bytes);
                    if (cached is not null || typeof(T).IsValueType == false)
                    {
                        _logger.LogDebug("Cache hit for key {CacheKey}.", key);
                        return cached!;
                    }
                }

                _logger.LogDebug("Cache miss for key {CacheKey}.", key);

                var value = await factory(cancellationToken).ConfigureAwait(false);
                await SetAsync(key, value, expiration, cancellationToken).ConfigureAwait(false);
                return value;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Cache get-or-create failed for key {CacheKey}.", key);
                return await factory(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public Task InvalidateListingsAsync(CancellationToken cancellationToken)
        => SetAsync(MemoryCacheService.ListingCacheTokenKey, Guid.NewGuid().ToString("N"), null, cancellationToken);

    /// <inheritdoc />
    public Task InvalidateCommissionRatesAsync(CancellationToken cancellationToken)
        => SetAsync(MemoryCacheService.CommissionRateCacheTokenKey, Guid.NewGuid().ToString("N"), null, cancellationToken);

    internal static class Keys
    {
        public const string ListingCacheTokenKey = "cache:token:listings";
        public const string CommissionRateCacheTokenKey = "cache:token:commission-rates";
    }
}
