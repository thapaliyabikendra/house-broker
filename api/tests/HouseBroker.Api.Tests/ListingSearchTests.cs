using HouseBroker.Application.Contracts;
using HouseBroker.Infrastructure;
using HouseBroker.Infrastructure.Persistence;
using HouseBroker.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>Listing search criteria composition tests.</summary>
public sealed class ListingSearchTests
{
    private static async Task<AppDbContext> CreateContextAsync(string databaseName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;MultipleActiveResultSets=True;")
            .Options;

        var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    /// <summary>Only active listings are returned.</summary>
    [Fact]
    public async Task SearchAsync_ActiveOnly_ReturnsActiveListings()
    {
        var database = $"ListingSearch_{Guid.NewGuid():N}";
        var context = await CreateContextAsync(database);
        try
        {
            var active = ListingBuilder.Create();
            active.Publish();
            var draft = ListingBuilder.Create();
            context.Listings.AddRange(active, draft);
            await context.SaveChangesAsync();

            var queries = new ListingQueries(context);
            var result = await queries.SearchAsync(new ListingSearchQuery(), CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal(active.Id, result.Items[0].Id);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>Soft-deleted active listings are excluded.</summary>
    [Fact]
    public async Task SearchAsync_SoftDeleted_Excluded()
    {
        var database = $"ListingSearch_{Guid.NewGuid():N}";
        var context = await CreateContextAsync(database);
        try
        {
            var active = ListingBuilder.Create();
            active.Publish();
            var deleted = ListingBuilder.Create();
            deleted.Publish();
            deleted.SoftDelete();
            context.Listings.AddRange(active, deleted);
            await context.SaveChangesAsync();

            var queries = new ListingQueries(context);
            var result = await queries.SearchAsync(new ListingSearchQuery(), CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal(active.Id, result.Items[0].Id);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
            await context.DisposeAsync();
        }
    }
}
