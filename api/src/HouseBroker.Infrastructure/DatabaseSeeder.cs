using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using HouseBroker.Infrastructure.Persistence;
using HouseBroker.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace HouseBroker.Infrastructure;

/// <summary>Idempotent database seeder.</summary>
public static class DatabaseSeeder
{
    private static readonly byte[] DemoImage =
        Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR4nGNgYAoADQABD1v9RgAAAABJRU5ErkJggg==");

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
        var tiers = await db.CommissionRates.AsNoTracking().ToListAsync(cancellationToken);

        await EnsureListingAsync(db, clock, tiers, DemoUsers.BrokerOne,
            title: "Demo Apartment",
            description: "A seeded demonstration listing in Thamel.",
            propertyTypeCode: "Apartment",
            price: 4_500_000m,
            province: "Bagmati",
            city: "Kathmandu",
            locality: "Thamel",
            postalCode: "44600",
            bedrooms: 2,
            bathrooms: 2,
            areaSqFt: 1100m,
            featureCodes: ["Parking", "Security"],
            status: ListingStatus.Draft,
            addImage: true,
            cancellationToken);

        await EnsureListingAsync(db, clock, tiers, DemoUsers.BrokerOne,
            title: "Family House in Bhaktapur",
            description: "A spacious house suited to a growing family, within commuting distance of Kathmandu.",
            propertyTypeCode: "House",
            price: 8_750_000m,
            province: "Bagmati",
            city: "Bhaktapur",
            locality: "Suryabinayak",
            postalCode: "44800",
            bedrooms: 4,
            bathrooms: 3,
            areaSqFt: 2200m,
            featureCodes: ["Parking", "Garden", "Security"],
            status: ListingStatus.Active,
            addImage: true,
            cancellationToken);

        await EnsureListingAsync(db, clock, tiers, DemoUsers.BrokerTwo,
            title: "Prime Commercial Space in New Road",
            description: "Ground-floor retail unit in a high-footfall heritage commercial area.",
            propertyTypeCode: "Commercial",
            price: 32_000_000m,
            province: "Bagmati",
            city: "Kathmandu",
            locality: "New Road",
            postalCode: "44600",
            bedrooms: 0,
            bathrooms: 2,
            areaSqFt: 1800m,
            featureCodes: ["Parking", "Generator", "Security"],
            status: ListingStatus.Active,
            addImage: true,
            cancellationToken);

        await EnsureListingAsync(db, clock, tiers, DemoUsers.BrokerOne,
            title: "Compact Apartment in Lalitpur",
            description: "Well-lit apartment close to cafes and public transport.",
            propertyTypeCode: "Apartment",
            price: 4_999_999m,
            province: "Bagmati",
            city: "Lalitpur",
            locality: "Jawalakhel",
            postalCode: "44700",
            bedrooms: 2,
            bathrooms: 1,
            areaSqFt: 980m,
            featureCodes: ["Parking"],
            status: ListingStatus.Draft,
            addImage: false,
            cancellationToken);

        await EnsureListingAsync(db, clock, tiers, DemoUsers.BrokerTwo,
            title: "Agricultural Land in Chitwan",
            description: "Fertile land along a paved road, suitable for residential conversion.",
            propertyTypeCode: "Land",
            price: 12_500_000m,
            province: "Bagmati",
            city: "Bharatpur",
            locality: "Chitwan",
            postalCode: "44200",
            bedrooms: 0,
            bathrooms: 0,
            areaSqFt: 5000m,
            featureCodes: ["Garden"],
            status: ListingStatus.Active,
            addImage: true,
            cancellationToken);

        await EnsureListingAsync(db, clock, tiers, DemoUsers.BrokerOne,
            title: "Boundary Value Land at Exactly Five Million",
            description: "Seeded to exercise the inclusive commission-tier boundary at 50,00,000.",
            propertyTypeCode: "Land",
            price: 5_000_000m,
            province: "Bagmati",
            city: "Kathmandu",
            locality: "Tokha",
            postalCode: "44600",
            bedrooms: 0,
            bathrooms: 0,
            areaSqFt: 1000m,
            featureCodes: [],
            status: ListingStatus.Draft,
            addImage: false,
            cancellationToken);
    }

    private static async Task EnsureListingAsync(
        AppDbContext db,
        TimeProvider clock,
        List<CommissionRate> tiers,
        Guid brokerId,
        string title,
        string description,
        string propertyTypeCode,
        decimal price,
        string province,
        string city,
        string locality,
        string postalCode,
        int bedrooms,
        int bathrooms,
        decimal areaSqFt,
        string[] featureCodes,
        ListingStatus status,
        bool addImage,
        CancellationToken cancellationToken)
    {
        if (await db.Listings.IgnoreQueryFilters().AnyAsync(item => item.BrokerId == brokerId && item.Title == title, cancellationToken))
        {
            return;
        }

        var propertyType = await db.PropertyTypes.AsNoTracking().FirstOrDefaultAsync(item => item.Code == propertyTypeCode, cancellationToken);
        if (propertyType is null)
        {
            return;
        }

        var featureIds = await db.Features.AsNoTracking()
            .Where(item => featureCodes.Contains(item.Code))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var listing = Listing.Create(
            brokerId,
            title,
            description,
            propertyType.Id,
            price,
            province,
            city,
            locality,
            postalCode,
            bedrooms,
            bathrooms,
            areaSqFt);

        listing.ReplaceFeatures(featureIds);
        if (status != ListingStatus.Draft)
        {
            listing.Publish();
        }

        var commission = CommissionCalculator.Calculate(price, tiers, clock.GetUtcNow().UtcDateTime);
        listing.ApplyCommission(commission.Amount, commission.RatePercent, commission.RateId);

        db.Listings.Add(listing);

        if (addImage)
        {
            var image = ListingImage.Create(listing.Id, "seed-photo.jpg", "image/jpeg", DemoImage.Length, "seed-image-hash", true);
            db.ListingImages.Add(image);
            db.ListingImageData.Add(ListingImageData.Create(image.Id, DemoImage));
        }
    }
}