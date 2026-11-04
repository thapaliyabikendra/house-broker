using HouseBroker.Application.Contracts;
using HouseBroker.Infrastructure;
using HouseBroker.Infrastructure.Persistence;
using HouseBroker.Domain;
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

    /// <summary>Search filters by price range.</summary>
    [Fact]
    public async Task SearchAsync_PriceRange_FiltersListings()
    {
        var database = $"ListingSearch_{Guid.NewGuid():N}";
        var context = await CreateContextAsync(database);
        try
        {
            var cheap = Listing.Create(Guid.NewGuid(), "Cheap", "D", Guid.NewGuid(), 100m, "P", "City", "L", "12345", 1, 1, 50m);
            cheap.Publish();
            var expensive = Listing.Create(Guid.NewGuid(), "Expensive", "D", Guid.NewGuid(), 1000m, "P", "City", "L", "12345", 1, 1, 50m);
            expensive.Publish();
            context.Listings.AddRange(cheap, expensive);
            await context.SaveChangesAsync();

            var queries = new ListingQueries(context);
            var result = await queries.SearchAsync(new ListingSearchQuery { MinPrice = 500m, MaxPrice = 2000m }, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal(expensive.Id, result.Items[0].Id);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>Search filters by location text.</summary>
    [Fact]
    public async Task SearchAsync_Location_FiltersListings()
    {
        var database = $"ListingSearch_{Guid.NewGuid():N}";
        var context = await CreateContextAsync(database);
        try
        {
            var local = Listing.Create(Guid.NewGuid(), "Local", "D", Guid.NewGuid(), 100m, "Bagmati", "Kathmandu", "Thamel", "12345", 1, 1, 50m);
            local.Publish();
            var remote = Listing.Create(Guid.NewGuid(), "Remote", "D", Guid.NewGuid(), 100m, "Gandaki", "Pokhara", "Lakeside", "12345", 1, 1, 50m);
            remote.Publish();
            context.Listings.AddRange(local, remote);
            await context.SaveChangesAsync();

            var queries = new ListingQueries(context);
            var result = await queries.SearchAsync(new ListingSearchQuery { Location = "Kathmandu" }, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal(local.Id, result.Items[0].Id);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>Search sorts by price ascending.</summary>
    [Fact]
    public async Task SearchAsync_SortPriceAscending_OrdersListings()
    {
        var database = $"ListingSearch_{Guid.NewGuid():N}";
        var context = await CreateContextAsync(database);
        try
        {
            var high = Listing.Create(Guid.NewGuid(), "High", "D", Guid.NewGuid(), 900m, "P", "City", "L", "12345", 1, 1, 50m);
            high.Publish();
            var low = Listing.Create(Guid.NewGuid(), "Low", "D", Guid.NewGuid(), 100m, "P", "City", "L", "12345", 1, 1, 50m);
            low.Publish();
            context.Listings.AddRange(high, low);
            await context.SaveChangesAsync();

            var queries = new ListingQueries(context);
            var result = await queries.SearchAsync(new ListingSearchQuery { Sort = "price_asc" }, CancellationToken.None);

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(low.Id, result.Items[0].Id);
            Assert.Equal(high.Id, result.Items[1].Id);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
            await context.DisposeAsync();
        }
    }

    /// <summary>Search paginates results.</summary>
    [Fact]
    public async Task SearchAsync_Paging_ReturnsExpectedPage()
    {
        var database = $"ListingSearch_{Guid.NewGuid():N}";
        var context = await CreateContextAsync(database);
        try
        {
            for (var index = 0; index < 3; index++)
            {
                var listing = Listing.Create(Guid.NewGuid(), $"L{index}", "D", Guid.NewGuid(), 100m + index, "P", "City", "L", "12345", 1, 1, 50m);
                listing.Publish();
                context.Listings.Add(listing);
            }
            await context.SaveChangesAsync();

            var queries = new ListingQueries(context);
            var result = await queries.SearchAsync(new ListingSearchQuery { Page = 2, PageSize = 2 }, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal(2, result.Page);
        }
        finally
        {
            await context.Database.EnsureDeletedAsync();
            await context.DisposeAsync();
        }
    }
}