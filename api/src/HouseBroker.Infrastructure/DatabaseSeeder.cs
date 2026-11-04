using HouseBroker.Application;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HouseBroker.Infrastructure;

/// <summary>Idempotent database seeder.</summary>
public static class DatabaseSeeder
{
    private static readonly byte[] DemoImageBytes = LoadDemoImageBytes();

    private static byte[] LoadDemoImageBytes()
    {
        const string resourceName = "HouseBroker.Infrastructure.SeedData.demo-house.jpg";
        using var stream = typeof(DatabaseSeeder).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");
        using var memory = new System.IO.MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

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
            await SeedDemoListingsAsync(db, clock, cancellationToken);
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

    private static async Task SeedDemoListingsAsync(AppDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        if (await db.Listings.IgnoreQueryFilters().AnyAsync(item => item.BrokerId == DemoUsers.BrokerOne, cancellationToken)
            || await db.Listings.IgnoreQueryFilters().AnyAsync(item => item.BrokerId == DemoUsers.BrokerTwo, cancellationToken))
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var tiers = await db.CommissionRates.AsNoTracking().ToListAsync(cancellationToken);

        var propertyTypes = await db.PropertyTypes.AsNoTracking().ToDictionaryAsync(item => item.Code, item => item, cancellationToken);
        var features = await db.Features.AsNoTracking().ToDictionaryAsync(item => item.Code, item => item, cancellationToken);

        var seeds = new[]
        {
            new DemoListingSeed
            {
                BrokerId = DemoUsers.BrokerOne,
                Title = "Demo Apartment",
                Description = "A seeded apartment listing below the first commission band.",
                PropertyTypeCode = "Apartment",
                Price = 4_000_000m,
                Province = "Bagmati",
                City = "Kathmandu",
                Locality = "Thamel",
                PostalCode = "44600",
                Bedrooms = 2,
                Bathrooms = 2,
                AreaSqFt = 1100m,
                FeatureCodes = new[] { "Parking", "Security" },
                ImageCount = 2,
            },
            new DemoListingSeed
            {
                BrokerId = DemoUsers.BrokerOne,
                Title = "Demo Family House",
                Description = "A seeded family house within the middle commission band.",
                PropertyTypeCode = "House",
                Price = 7_500_000m,
                Province = "Bagmati",
                City = "Lalitpur",
                Locality = "Jawalakhel",
                PostalCode = "44700",
                Bedrooms = 4,
                Bathrooms = 3,
                AreaSqFt = 2200m,
                FeatureCodes = new[] { "Parking", "Garden" },
                ImageCount = 2,
            },
            new DemoListingSeed
            {
                BrokerId = DemoUsers.BrokerTwo,
                Title = "Demo Commercial Space",
                Description = "A seeded commercial listing above the top commission band.",
                PropertyTypeCode = "Commercial",
                Price = 15_000_000m,
                Province = "Bagmati",
                City = "Bhaktapur",
                Locality = "Suryabinayak",
                PostalCode = "44800",
                Bedrooms = 0,
                Bathrooms = 2,
                AreaSqFt = 3500m,
                FeatureCodes = new[] { "Security", "Generator", "Parking" },
                ImageCount = 2,
            },
            new DemoListingSeed
            {
                BrokerId = DemoUsers.BrokerTwo,
                Title = "Demo Land Plot",
                Description = "A seeded land parcel within the middle commission band.",
                PropertyTypeCode = "Land",
                Price = 9_000_000m,
                Province = "Gandaki",
                City = "Pokhara",
                Locality = "Lakeside",
                PostalCode = "33700",
                Bedrooms = 0,
                Bathrooms = 0,
                AreaSqFt = 4000m,
                FeatureCodes = new[] { "Parking", "Garden" },
                ImageCount = 1,
            },
        };

        foreach (var seed in seeds)
        {
            if (!propertyTypes.TryGetValue(seed.PropertyTypeCode, out var propertyType))
            {
                continue;
            }

            var listing = Listing.Create(
                seed.BrokerId,
                seed.Title,
                seed.Description,
                propertyType.Id,
                seed.Price,
                seed.Province,
                seed.City,
                seed.Locality,
                seed.PostalCode,
                seed.Bedrooms,
                seed.Bathrooms,
                seed.AreaSqFt);

            foreach (var featureCode in seed.FeatureCodes)
            {
                if (features.TryGetValue(featureCode, out var feature))
                {
                    listing.AddFeature(feature.Id);
                }
            }

            var commission = CommissionCalculator.Calculate(seed.Price, tiers, now);
            listing.ApplyCommission(commission.Amount, commission.RatePercent, commission.RateId);

            for (var index = 0; index < seed.ImageCount; index++)
            {
                var image = ListingImage.Create(
                    listing.Id,
                    $"demo-image-{index + 1}.jpg",
                    "image/jpeg",
                    DemoImageBytes.Length,
                    "demo-image-hash",
                    index == 0);

                listing.AddImage(image);
                db.ListingImageData.Add(ListingImageData.Create(image.Id, DemoImageBytes));
            }

            var fromStatus = listing.Status;
            listing.Publish();
            db.ListingStatusHistories.Add(
                ListingStatusHistory.Create(listing.Id, fromStatus, listing.Status, null, now, "Seeded demo listing"));

            db.Listings.Add(listing);
        }
    }

    private sealed class DemoListingSeed
    {
        public Guid BrokerId { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string PropertyTypeCode { get; init; } = string.Empty;

        public decimal Price { get; init; }

        public string Province { get; init; } = string.Empty;

        public string City { get; init; } = string.Empty;

        public string Locality { get; init; } = string.Empty;

        public string PostalCode { get; init; } = string.Empty;

        public int Bedrooms { get; init; }

        public int Bathrooms { get; init; }

        public decimal AreaSqFt { get; init; }

        public string[] FeatureCodes { get; init; } = Array.Empty<string>();


        public int ImageCount { get; init; }
    }
}
