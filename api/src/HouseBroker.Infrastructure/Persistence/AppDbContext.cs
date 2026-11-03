using HouseBroker.Domain;
using Microsoft.EntityFrameworkCore;

namespace HouseBroker.Infrastructure.Persistence;

/// <summary>Application database context.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), HouseBroker.Application.Contracts.IUnitOfWork
{
    /// <summary>Gets listings.</summary>
    public DbSet<Listing> Listings => Set<Listing>();

    /// <summary>Gets listing images.</summary>
    public DbSet<ListingImage> ListingImages => Set<ListingImage>();

    /// <summary>Gets listing image data.</summary>
    public DbSet<ListingImageData> ListingImageData => Set<ListingImageData>();

    /// <summary>Gets listing features.</summary>
    public DbSet<ListingFeature> ListingFeatures => Set<ListingFeature>();

    /// <summary>Gets property types.</summary>
    public DbSet<PropertyType> PropertyTypes => Set<PropertyType>();

    /// <summary>Gets features.</summary>
    public DbSet<Feature> Features => Set<Feature>();

    /// <summary>Gets broker profiles.</summary>
    public DbSet<BrokerProfile> BrokerProfiles => Set<BrokerProfile>();

    /// <summary>Gets commission rates.</summary>
    public DbSet<CommissionRate> CommissionRates => Set<CommissionRate>();

    /// <summary>Gets listing status histories.</summary>
    public DbSet<ListingStatusHistory> ListingStatusHistories => Set<ListingStatusHistory>();

    /// <summary>Gets listing price histories.</summary>
    public DbSet<ListingPriceHistory> ListingPriceHistories => Set<ListingPriceHistory>();

    /// <summary>Gets audit events.</summary>
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureListing(modelBuilder);
        ConfigureListingImage(modelBuilder);
        ConfigureListingImageData(modelBuilder);
        ConfigureListingFeature(modelBuilder);
        ConfigureLookups(modelBuilder);
        ConfigureBrokerProfile(modelBuilder);
        ConfigureCommissionRate(modelBuilder);
        ConfigureHistory(modelBuilder);
        ConfigureAuditEvent(modelBuilder);

        modelBuilder.Entity<Listing>().HasQueryFilter(listing => !listing.IsDeleted);
    }

    private static void ConfigureListing(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Listing>();
        entity.ToTable("Listings");
        entity.HasKey(listing => listing.Id);
        entity.Property(listing => listing.Id).ValueGeneratedNever();
        entity.Property(listing => listing.BrokerId).IsRequired();
        entity.Property(listing => listing.Title).IsRequired().HasMaxLength(Domain.Shared.Limits.MaxTitleLength);
        entity.Property(listing => listing.Description).IsRequired();
        entity.Property(listing => listing.Price).HasPrecision(18, 2);
        entity.Property(listing => listing.Province).IsRequired().HasMaxLength(100);
        entity.Property(listing => listing.City).IsRequired().HasMaxLength(100);
        entity.Property(listing => listing.Locality).IsRequired().HasMaxLength(100);
        entity.Property(listing => listing.PostalCode).IsRequired().HasMaxLength(10);
        entity.Property(listing => listing.AreaSqFt).HasPrecision(18, 2);
        entity.Property(listing => listing.Status).HasConversion<int>();
        entity.Property(listing => listing.CommissionAmount).HasPrecision(18, 2);
        entity.Property(listing => listing.CommissionRatePercent).HasPrecision(9, 4);
        entity.Property(listing => listing.CreatedAt).IsRequired();
        entity.Property(listing => listing.UpdatedAt).IsRequired();
        entity.Property(listing => listing.IsDeleted).IsRequired();
        entity.Property(listing => listing.RowVersion).IsRowVersion();
        entity.HasIndex(listing => new { listing.City, listing.Locality });
        entity.HasIndex(listing => listing.Price);
        entity.HasIndex(listing => listing.PropertyTypeId);
        entity.HasIndex(listing => listing.Status);
        entity.HasIndex(listing => listing.IsDeleted);
    }

    private static void ConfigureListingImage(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ListingImage>();
        entity.ToTable("ListingImages");
        entity.HasKey(image => image.Id);
        entity.Property(image => image.Id).ValueGeneratedNever();
        entity.Property(image => image.FileName).IsRequired().HasMaxLength(260);
        entity.Property(image => image.ContentType).IsRequired().HasMaxLength(100);
        entity.Property(image => image.ContentLength).IsRequired();
        entity.Property(image => image.ContentHash).IsRequired().HasMaxLength(128);
        entity.Property(image => image.CreatedAt).IsRequired();
        entity.Property(image => image.UpdatedAt).IsRequired();
        entity.HasOne<Listing>().WithMany().HasForeignKey(image => image.ListingId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureListingImageData(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ListingImageData>();
        entity.ToTable("ListingImageData");
        entity.HasKey(data => data.ListingImageId);
        entity.Property(data => data.Content).IsRequired();
        entity.HasOne<ListingImage>().WithOne().HasForeignKey<ListingImageData>(data => data.ListingImageId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureListingFeature(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ListingFeature>();
        entity.ToTable("ListingFeatures");
        entity.HasKey(feature => feature.Id);
        entity.Property(feature => feature.Id).ValueGeneratedNever();
        entity.Property(feature => feature.CreatedAt).IsRequired();
        entity.HasIndex(feature => new { feature.ListingId, feature.FeatureId }).IsUnique();
        entity.HasOne<Listing>().WithMany().HasForeignKey(feature => feature.ListingId).OnDelete(DeleteBehavior.Cascade);
        entity.HasOne<Feature>().WithMany().HasForeignKey(feature => feature.FeatureId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureLookups(ModelBuilder modelBuilder)
    {
        var propertyType = modelBuilder.Entity<PropertyType>();
        propertyType.ToTable("PropertyTypes");
        propertyType.HasKey(item => item.Id);
        propertyType.Property(item => item.Id).ValueGeneratedNever();
        propertyType.Property(item => item.Code).IsRequired().HasMaxLength(100);
        propertyType.Property(item => item.Name).IsRequired().HasMaxLength(200);
        propertyType.Property(item => item.SortOrder).IsRequired();
        propertyType.Property(item => item.CreatedAt).IsRequired();
        propertyType.Property(item => item.UpdatedAt).IsRequired();
        propertyType.HasIndex(item => item.Code).IsUnique();

        var feature = modelBuilder.Entity<Feature>();
        feature.ToTable("Features");
        feature.HasKey(item => item.Id);
        feature.Property(item => item.Id).ValueGeneratedNever();
        feature.Property(item => item.Code).IsRequired().HasMaxLength(100);
        feature.Property(item => item.Name).IsRequired().HasMaxLength(200);
        feature.Property(item => item.SortOrder).IsRequired();
        feature.Property(item => item.CreatedAt).IsRequired();
        feature.Property(item => item.UpdatedAt).IsRequired();
        feature.HasIndex(item => item.Code).IsUnique();
    }

    private static void ConfigureBrokerProfile(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<BrokerProfile>();
        entity.ToTable("BrokerProfiles");
        entity.HasKey(profile => profile.BrokerId);
        entity.Property(profile => profile.BrokerId).ValueGeneratedNever();
        entity.Property(profile => profile.FullName).IsRequired().HasMaxLength(200);
        entity.Property(profile => profile.Email).HasMaxLength(320);
        entity.Property(profile => profile.PhoneNumber).HasMaxLength(50);
        entity.Property(profile => profile.CreatedAt).IsRequired();
        entity.Property(profile => profile.UpdatedAt).IsRequired();
    }

    private static void ConfigureCommissionRate(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CommissionRate>();
        entity.ToTable("CommissionRates");
        entity.HasKey(rate => rate.Id);
        entity.Property(rate => rate.Id).ValueGeneratedNever();
        entity.Property(rate => rate.MinPrice).HasPrecision(18, 2);
        entity.Property(rate => rate.MaxPrice).HasPrecision(18, 2);
        entity.Property(rate => rate.RatePercent).HasPrecision(9, 4);
        entity.Property(rate => rate.EffectiveFrom).IsRequired();
        entity.Property(rate => rate.CreatedAt).IsRequired();
        entity.Property(rate => rate.UpdatedAt).IsRequired();
        entity.Property(rate => rate.RowVersion).IsRowVersion();
    }

    private static void ConfigureHistory(ModelBuilder modelBuilder)
    {
        var status = modelBuilder.Entity<ListingStatusHistory>();
        status.ToTable("ListingStatusHistories");
        status.HasKey(history => history.Id);
        status.Property(history => history.Id).ValueGeneratedNever();
        status.Property(history => history.ChangedAt).IsRequired();
        status.HasIndex(history => new { history.ListingId, history.ChangedAt });

        var price = modelBuilder.Entity<ListingPriceHistory>();
        price.ToTable("ListingPriceHistories");
        price.HasKey(history => history.Id);
        price.Property(history => history.Id).ValueGeneratedNever();
        price.Property(history => history.OldPrice).HasPrecision(18, 2);
        price.Property(priceHistory => priceHistory.NewPrice).HasPrecision(18, 2);
        price.Property(priceHistory => priceHistory.OldCommissionAmount).HasPrecision(18, 2);
        price.Property(priceHistory => priceHistory.NewCommissionAmount).HasPrecision(18, 2);
        price.Property(priceHistory => priceHistory.OldCommissionRatePercent).HasPrecision(9, 4);
        price.Property(priceHistory => priceHistory.NewCommissionRatePercent).HasPrecision(9, 4);
        price.Property(priceHistory => priceHistory.ChangedAt).IsRequired();
        price.HasIndex(priceHistory => new { priceHistory.ListingId, priceHistory.ChangedAt });
    }

    private static void ConfigureAuditEvent(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AuditEvent>();
        entity.ToTable("AuditEvents");
        entity.HasKey(audit => audit.Id);
        entity.Property(audit => audit.Id).ValueGeneratedNever();
        entity.Property(audit => audit.ActorType).IsRequired().HasMaxLength(50);
        entity.Property(audit => audit.Action).IsRequired().HasMaxLength(100);
        entity.Property(audit => audit.EntityType).IsRequired().HasMaxLength(100);
        entity.Property(audit => audit.OccurredAt).IsRequired();
        entity.HasIndex(audit => audit.OccurredAt);
    }
}