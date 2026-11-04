using HouseBroker.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>Contract tests for the memory cache provider.</summary>
public sealed class MemoryCacheServiceContractTests
{
    /// <summary>Creates the service with a disposable memory cache.</summary>
    private static MemoryCacheService CreateService(out MemoryCache memoryCache)
    {
        memoryCache = new MemoryCache(new MemoryCacheOptions());
        return new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);
    }

    /// <summary>Missing keys return the default value.</summary>
    [Fact]
    public async Task GetAsync_MissingKey_ReturnsDefault()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);

        var value = await cache.GetAsync<int>("missing", CancellationToken.None);

        Assert.Equal(default, value);
    }

    /// <summary>Set values can be read back.</summary>
    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsValue()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);

        await cache.SetAsync("key", 42, TimeSpan.FromMinutes(1), CancellationToken.None);
        var value = await cache.GetAsync<int>("key", CancellationToken.None);

        Assert.Equal(42, value);
    }

    /// <summary>Factory results are cached for subsequent calls.</summary>
    [Fact]
    public async Task GetOrCreateAsync_CachesFactoryResult()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);
        var calls = 0;

        Func<CancellationToken, Task<int>> factory = async cancellationToken =>
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(10, cancellationToken);
            return 42;
        };

        var first = await cache.GetOrCreateAsync("key", factory, TimeSpan.FromMinutes(1), CancellationToken.None);
        var second = await cache.GetOrCreateAsync("key", factory, TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal(42, first);
        Assert.Equal(42, second);
        Assert.Equal(1, calls);
    }

    /// <summary>Factory failures surface and the lock is released for retries.</summary>
    [Fact]
    public async Task GetOrCreateAsync_FactoryThrows_CallsFactoryAgainOnRetry()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);
        var calls = 0;

        Func<CancellationToken, Task<int>> failingFactory = _ =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("boom");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.GetOrCreateAsync("key", failingFactory, null, CancellationToken.None));

        var value = await cache.GetOrCreateAsync("key", _ => Task.FromResult(7), null, CancellationToken.None);

        Assert.Equal(7, value);
        Assert.True(calls >= 1);
    }

    /// <summary>Removed entries can no longer be read.</summary>
    [Fact]
    public async Task RemoveAsync_RemovesCachedValue()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new MemoryCacheService(memoryCache, NullLogger<MemoryCacheService>.Instance);

        await cache.SetAsync("key", 42, TimeSpan.FromMinutes(1), CancellationToken.None);
        await cache.RemoveAsync("key", CancellationToken.None);
        var value = await cache.GetAsync<int>("key", CancellationToken.None);

        Assert.Equal(default, value);
    }
}
