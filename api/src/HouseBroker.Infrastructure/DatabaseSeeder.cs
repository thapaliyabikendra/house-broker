using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HouseBroker.Infrastructure;

/// <summary>Idempotent database seeder.</summary>
public static class DatabaseSeeder
{
    private static readonly Guid[] DemoListingIds =
    [
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
    ];

    /// <summary>Seeds master and development data.</summary>
    public static async Task SeedAsync(AppDbContext db, IHostEnvironment environment, TimeProvider clock, CancellationToken cancellationToken)
    {
        await SeedPropertyTypesAsync(db, cancellationToken);
        await SeedFeaturesAsync(db, cancellationToken);
        await SeedCommissionRatesAsync(db, clock, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (environment.IsDevelopment())
        {
            await SeedDemoBrokerProfilesAsync(db, cancellationToken);
            await SeedDemoListingsAsync(db, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedPropertyTypesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var seeds = new (string Code, string Name, int SortOrder)[]
        {
            ("Apartment", "Apartment", 1),
            ("House", "House", 2),
            ("Land", "Land", 3),
            ("Commercial", "Commercial", 4),
        };

        foreach (var seed in seeds)
        {
            if (!await db.PropertyTypes.AnyAsync(item => item.Code == seed.Code, cancellationToken))
            {
                db.PropertyTypes.Add(PropertyType.Create(seed.Code, seed.Name, seed.SortOrder));
            }
        }
    }

    private static async Task SeedFeaturesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var seeds = new (string Code, string Name, int SortOrder)[]
        {
            ("Parking", "Parking", 1),
            ("Garden", "Garden", 2),
            ("Security", "Security", 3),
            ("Generator", "Generator", 4),
        };

        foreach (var seed in seeds)
        {
            if (!await db.Features.AnyAsync(item => item.Code == seed.Code, cancellationToken))
            {
                db.Features.Add(Feature.Create(seed.Code, seed.Name, seed.SortOrder));
            }
        }
    }

    private static async Task SeedCommissionRatesAsync(AppDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (await db.CommissionRates.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;

        db.CommissionRates.AddRange(
            CommissionRate.Create(0m, true, 5_000_000m, false, 2.00m, true, now),
            CommissionRate.Create(5_000_000m, true, 10_000_000m, true, 1.75m, true, now),
            CommissionRate.Create(10_000_000m, false, null, false, 1.50m, true, now));
    }

    private static async Task SeedDemoBrokerProfilesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.BrokerProfiles.AnyAsync(item => item.BrokerId == DemoUsers.BrokerOne, cancellationToken))
        {
            db.BrokerProfiles.Add(BrokerProfile.Create(DemoUsers.BrokerOne, "Demo Broker One", "broker1@example.com", "9800000001"));
        }

        if (!await db.BrokerProfiles.AnyAsync(item => item.BrokerId == DemoUsers.BrokerTwo, cancellationToken))
        {
            db.BrokerProfiles.Add(BrokerProfile.Create(DemoUsers.BrokerTwo, "Demo Broker Two", "broker2@example.com", "9800000002"));
        }
    }

    private static async Task SeedDemoListingsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Listings.IgnoreQueryFilters().AnyAsync(item => item.BrokerId == DemoUsers.BrokerOne, cancellationToken))
        {
            return;
        }

        var propertyType = await db.PropertyTypes.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        if (propertyType is null)
        {
            return;
        }

        var listing = Listing.Create(
            DemoUsers.BrokerOne,
            "Demo Apartment",
            "A seeded demonstration listing.",
            propertyType.Id,
            4_500_000m,
            "Bagmati",
            "Kathmandu",
            "Thamel",
            "44600",
            2,
            2,
            1100m);

        db.Listings.Add(listing);
    }
}
