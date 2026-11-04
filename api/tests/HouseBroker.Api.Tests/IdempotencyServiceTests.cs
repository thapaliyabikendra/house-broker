using HouseBroker.Application.Contracts;
using HouseBroker.Infrastructure.Idempotency;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>Idempotency service tests.</summary>
public sealed class IdempotencyServiceTests
{
    private sealed class FakeClock(DateTime utcNow) : TimeProvider
    {
        public DateTime UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
    }

    private static async Task<AppDbContext> CreateContextAsync(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=True;")
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    /// <summary>New begin returns new.</summary>
    [Fact]
    public async Task BeginAsync_NewKey_ReturnsNew()
    {
        var database = $"Idempotency_{Guid.NewGuid():N}";
        using var context = await CreateContextAsync(database);
        var service = new IdempotencyService(context, new FakeClock(DateTime.UtcNow));

        var result = await service.BeginAsync("key", "scope", "hash", CancellationToken.None);

        Assert.Equal(IdempotencyStatus.New, result.Status);
    }

    /// <summary>Replay returns stored result.</summary>
    [Fact]
    public async Task BeginAsync_CompletedKey_ReturnsReplay()
    {
        var database = $"Idempotency_{Guid.NewGuid():N}";
        using var context = await CreateContextAsync(database);
        var service = new IdempotencyService(context, new FakeClock(DateTime.UtcNow));

        await service.BeginAsync("key", "scope", "hash", CancellationToken.None);
        await service.CompleteAsync("key", "scope", 201, "stored body", CancellationToken.None);

        var result = await service.BeginAsync("key", "scope", "hash", CancellationToken.None);

        Assert.Equal(IdempotencyStatus.Replay, result.Status);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("stored body", result.ResponseBody);
    }

    /// <summary>Body mismatch returns conflict.</summary>
    [Fact]
    public async Task BeginAsync_DifferentHash_ReturnsConflict()
    {
        var database = $"Idempotency_{Guid.NewGuid():N}";
        using var context = await CreateContextAsync(database);
        var service = new IdempotencyService(context, new FakeClock(DateTime.UtcNow));

        await service.BeginAsync("key", "scope", "hash-one", CancellationToken.None);
        await service.CompleteAsync("key", "scope", 200, "first", CancellationToken.None);

        var result = await service.BeginAsync("key", "scope", "hash-two", CancellationToken.None);

        Assert.Equal(IdempotencyStatus.Conflict, result.Status);
    }

    /// <summary>In-progress returns in-progress.</summary>
    [Fact]
    public async Task BeginAsync_IncompleteKey_ReturnsInProgress()
    {
        var database = $"Idempotency_{Guid.NewGuid():N}";
        using var context = await CreateContextAsync(database);
        var service = new IdempotencyService(context, new FakeClock(DateTime.UtcNow));

        await service.BeginAsync("key", "scope", "hash", CancellationToken.None);

        var result = await service.BeginAsync("key", "scope", "hash", CancellationToken.None);

        Assert.Equal(IdempotencyStatus.InProgress, result.Status);
    }

    /// <summary>Expired key is replaced.</summary>
    [Fact]
    public async Task BeginAsync_ExpiredKey_ReturnsNew()
    {
        var database = $"Idempotency_{Guid.NewGuid():N}";
        var start = DateTime.UtcNow;
        using var context = await CreateContextAsync(database);
        var firstService = new IdempotencyService(context, new FakeClock(start));

        await firstService.BeginAsync("key", "scope", "hash", CancellationToken.None);

        var secondService = new IdempotencyService(context, new FakeClock(start.AddHours(25)));

        var result = await secondService.BeginAsync("key", "scope", "hash", CancellationToken.None);

        Assert.Equal(IdempotencyStatus.New, result.Status);
    }

    /// <summary>Per-user scope isolates keys.</summary>
    [Fact]
    public async Task BeginAsync_DifferentScopes_AreIndependent()
    {
        var database = $"Idempotency_{Guid.NewGuid():N}";
        using var context = await CreateContextAsync(database);
        var service = new IdempotencyService(context, new FakeClock(DateTime.UtcNow));

        var first = await service.BeginAsync("key", "user-one", "hash", CancellationToken.None);
        var second = await service.BeginAsync("key", "user-two", "hash", CancellationToken.None);

        Assert.Equal(IdempotencyStatus.New, first.Status);
        Assert.Equal(IdempotencyStatus.New, second.Status);
    }

    /// <summary>Failure releases the key.</summary>
    [Fact]
    public async Task ReleaseAsync_IncompleteKey_AllowsRestart()
    {
        var database = $"Idempotency_{Guid.NewGuid():N}";
        using var context = await CreateContextAsync(database);
        var service = new IdempotencyService(context, new FakeClock(DateTime.UtcNow));

        await service.BeginAsync("key", "scope", "hash", CancellationToken.None);
        await service.ReleaseAsync("key", "scope", CancellationToken.None);

        var result = await service.BeginAsync("key", "scope", "hash", CancellationToken.None);

        Assert.Equal(IdempotencyStatus.New, result.Status);
    }

    /// <summary>Completed keys are not released.</summary>
    [Fact]
    public async Task ReleaseAsync_CompletedKey_KeepsReplay()
    {
        var database = $"Idempotency_{Guid.NewGuid():N}";
        using var context = await CreateContextAsync(database);
        var service = new IdempotencyService(context, new FakeClock(DateTime.UtcNow));

        await service.BeginAsync("key", "scope", "hash", CancellationToken.None);
        await service.CompleteAsync("key", "scope", 200, "stored", CancellationToken.None);
        await service.ReleaseAsync("key", "scope", CancellationToken.None);

        var result = await service.BeginAsync("key", "scope", "hash", CancellationToken.None);

        Assert.Equal(IdempotencyStatus.Replay, result.Status);
    }

    /// <summary>ComputeHash is stable.</summary>
    [Fact]
    public void ComputeHash_SameInput_SameHash()
    {
        Assert.Equal(IdempotencyService.ComputeHash("body"), IdempotencyService.ComputeHash("body"));
        Assert.NotEqual(IdempotencyService.ComputeHash("body"), IdempotencyService.ComputeHash("other"));
    }
}
