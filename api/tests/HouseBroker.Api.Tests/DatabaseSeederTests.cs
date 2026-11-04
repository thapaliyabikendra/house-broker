using HouseBroker.Infrastructure;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>
/// Tests for database seeder behaviour.
/// </summary>
public class DatabaseSeederTests
{
    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    /// <summary>
    /// Seeds master data and remains idempotent.
    /// </summary>
    [Fact]
    public async Task SeedAsync_MasterData_IsPresentAndIdempotent()
    {
        using var db = CreateContext();
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };

        await DatabaseSeeder.SeedAsync(db, environment, TimeProvider.System, CancellationToken.None);
        var propertyTypesBefore = await db.PropertyTypes.CountAsync();
        var featuresBefore = await db.Features.CountAsync();
        var ratesBefore = await db.CommissionRates.CountAsync();
        var brokerProfilesBefore = await db.BrokerProfiles.CountAsync();
        var listingsBefore = await db.Listings.IgnoreQueryFilters().CountAsync();

        await DatabaseSeeder.SeedAsync(db, environment, TimeProvider.System, CancellationToken.None);

        Assert.Equal(4, propertyTypesBefore);
        Assert.Equal(4, featuresBefore);
        Assert.Equal(3, ratesBefore);
        Assert.Equal(0, brokerProfilesBefore);
        Assert.Equal(0, listingsBefore);
        Assert.Equal(propertyTypesBefore, await db.PropertyTypes.CountAsync());
        Assert.Equal(featuresBefore, await db.Features.CountAsync());
        Assert.Equal(ratesBefore, await db.CommissionRates.CountAsync());
    }

    /// <summary>
    /// Seeds demo data in development.
    /// </summary>
    [Fact]
    public async Task SeedAsync_Development_AddsDemoBrokerAndListing()
    {
        using var db = CreateContext();
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Development };

        await DatabaseSeeder.SeedAsync(db, environment, TimeProvider.System, CancellationToken.None);

        Assert.Equal(2, await db.BrokerProfiles.CountAsync());
        Assert.Equal(6, await db.Listings.IgnoreQueryFilters().CountAsync());
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "HouseBroker.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
