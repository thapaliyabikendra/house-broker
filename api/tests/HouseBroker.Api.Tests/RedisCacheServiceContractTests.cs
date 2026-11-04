using HouseBroker.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>Contract tests for the Redis cache provider.</summary>
public sealed class RedisCacheServiceContractTests
{
    private static RedisCacheService CreateService(out MemoryDistributedCache cache)
    {
        cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return new RedisCacheService(cache, NullLogger<RedisCacheService>.Instance);
    }

    /// <summary>Contract test.</summary>
    [Fact]
    public async Task GetAsync_MissingKey_ReturnsDefault()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = new RedisCacheService(cache, NullLogger<RedisCacheService>.Instance);

        var value = await service.GetAsync<int>("missing", CancellationToken.None);

        Assert.Equal(default, value);
    }

    /// <summary>Contract test.</summary>
    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsValue()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = new RedisCacheService(cache, NullLogger<RedisCacheService>.Instance);

        await service.SetAsync("key", 42, TimeSpan.FromMinutes(1), CancellationToken.None);
        var value = await service.GetAsync<int>("key", CancellationToken.None);

        Assert.Equal(42, value);
    }

    /// <summary>Contract test.</summary>
    [Fact]
    public async Task GetOrCreateAsync_CachesFactoryResult()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = new RedisCacheService(cache, NullLogger<RedisCacheService>.Instance);
        var calls = 0;

        Func<CancellationToken, Task<int>> factory = async cancellationToken =>
        {
            Interlocked.Increment(ref calls);
            await Task.Delay(10, cancellationToken);
            return 42;
        };

        var first = await service.GetOrCreateAsync("key", factory, TimeSpan.FromMinutes(1), CancellationToken.None);
        var second = await service.GetOrCreateAsync("key", factory, TimeSpan.FromMinutes(1), CancellationToken.None);

        Assert.Equal(42, first);
        Assert.Equal(42, second);
        Assert.Equal(1, calls);
    }

    /// <summary>Contract test.</summary>
    [Fact]
    public async Task GetOrCreateAsync_FactoryThrows_CallsFactoryAgainOnRetry()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = new RedisCacheService(cache, NullLogger<RedisCacheService>.Instance);
        var calls = 0;

        Func<CancellationToken, Task<int>> failingFactory = _ =>
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException("boom");
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetOrCreateAsync("key", failingFactory, null, CancellationToken.None));

        var value = await service.GetOrCreateAsync("key", _ => Task.FromResult(7), null, CancellationToken.None);

        Assert.Equal(7, value);
        Assert.True(calls >= 1);
    }

    /// <summary>Contract test.</summary>
    [Fact]
    public async Task RemoveAsync_RemovesCachedValue()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = new RedisCacheService(cache, NullLogger<RedisCacheService>.Instance);

        await service.SetAsync("key", 42, TimeSpan.FromMinutes(1), CancellationToken.None);
        await service.RemoveAsync("key", CancellationToken.None);
        var value = await service.GetAsync<int>("key", CancellationToken.None);

        Assert.Equal(default, value);
    }
}

