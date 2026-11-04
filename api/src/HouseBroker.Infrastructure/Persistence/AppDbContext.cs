using HouseBroker.Application.Contracts;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HouseBroker.Infrastructure.Persistence;

/// <summary>Application database context.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), HouseBroker.Application.Contracts.IUnitOfWork
{
    /// <summary>Saves changes and translates concurrency failures.</summary>
    async Task<Result<int>> IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return Result.Success<int>(await base.SaveChangesAsync(cancellationToken));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<int>(new ApplicationError(ErrorCodes.PreconditionFailed, ErrorKind.PreconditionFailed));
        }
    }

    /// <inheritdoc />
    async Task<HouseBroker.Application.Contracts.ITransactionScope> IUnitOfWork.BeginTransactionAsync(HouseBroker.Application.Contracts.TransactionIsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        var systemLevel = isolationLevel == HouseBroker.Application.Contracts.TransactionIsolationLevel.ReadUncommitted ? System.Data.IsolationLevel.ReadUncommitted : isolationLevel == HouseBroker.Application.Contracts.TransactionIsolationLevel.ReadCommitted ? System.Data.IsolationLevel.ReadCommitted : isolationLevel == HouseBroker.Application.Contracts.TransactionIsolationLevel.RepeatableRead ? System.Data.IsolationLevel.RepeatableRead : isolationLevel == HouseBroker.Application.Contracts.TransactionIsolationLevel.Snapshot ? System.Data.IsolationLevel.Snapshot : System.Data.IsolationLevel.Serializable;

        var transaction = await base.Database.BeginTransactionAsync(systemLevel, cancellationToken);
        return new EfTransactionScope(transaction);
    }

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

    /// <summary>Gets idempotency keys.</summary>
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeValueConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeValueConverter>();
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseCollation("Latin1_General_100_CI_AI");

        ConfigureListing(modelBuilder);
        ConfigureListingImage(modelBuilder);
        ConfigureListingImageData(modelBuilder);
        ConfigureListingFeature(modelBuilder);
        ConfigureLookups(modelBuilder);
        ConfigureBrokerProfile(modelBuilder);
        ConfigureCommissionRate(modelBuilder);
        ConfigureHistory(modelBuilder);
        ConfigureAuditEvent(modelBuilder);
        ConfigureIdempotencyKey(modelBuilder);

        modelBuilder.Entity<Listing>().HasQueryFilter(listing => !listing.IsDeleted);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                {
                    property.SetColumnType("datetime2(3)");
                    if (property.Name != nameof(Listing.DeletedAt))
                    {
                        property.SetDefaultValueSql("SYSUTCDATETIME()");
                    }
                    else
                    {
                        property.SetDefaultValueSql(null);
                    }
                }
            }
        }
    }

    private static void ConfigureListing(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Listing>();
        entity.ToTable("Listings");
        entity.HasKey(listing => listing.Id);
        entity.Property(listing => listing.Id).ValueGeneratedNever();
        entity.Property(listing => listing.BrokerId).IsRequired();
        entity.Property(listing => listing.Title).IsRequired().HasMaxLength(Domain.Common.Limits.MaxTitleLength);
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
        entity.HasIndex(listing => new { listing.City, listing.Locality }).HasFilter("[IsDeleted] = 0");
        entity.HasIndex(listing => listing.Price).HasFilter("[IsDeleted] = 0");
        entity.HasIndex(listing => listing.PropertyTypeId).HasFilter("[IsDeleted] = 0");
        entity.HasIndex(listing => listing.Status).HasFilter("[IsDeleted] = 0");
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
        entity.HasIndex(image => image.ListingId);
        entity.HasOne<Listing>()
            .WithMany(listing => listing.Images)
            .HasForeignKey(image => image.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
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
        entity.HasOne<Listing>()
            .WithMany(listing => listing.Features)
            .HasForeignKey(feature => feature.ListingId)
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(feature => feature.Feature)
            .WithMany()
            .HasForeignKey(feature => feature.FeatureId)
            .OnDelete(DeleteBehavior.Restrict);
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

    private static void ConfigureIdempotencyKey(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<IdempotencyKey>();
        entity.ToTable("IdempotencyKeys");
        entity.HasKey(key => key.Id);
        entity.Property(key => key.Id).ValueGeneratedNever();
        entity.Property(key => key.Key).IsRequired().HasMaxLength(128);
        entity.Property(key => key.Scope).IsRequired().HasMaxLength(128);
        entity.Property(key => key.RequestHash).IsRequired().HasMaxLength(128);
        entity.Property(key => key.ResponseBody).HasColumnType("nvarchar(max)");
        entity.Property(key => key.CreatedAt).IsRequired();
        entity.Property(key => key.UpdatedAt).IsRequired();
        entity.Property(key => key.ExpiresAt).IsRequired();
        entity.Property(key => key.RowVersion).IsRowVersion();
        entity.HasIndex(key => new { key.Key, key.Scope }).IsUnique();
        entity.HasIndex(key => key.ExpiresAt);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by EF Core convention configuration.")]
    private sealed class UtcDateTimeValueConverter() : ValueConverter<DateTime, DateTime>(
            value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:AvoidUninstantiatedInternalClasses", Justification = "Instantiated by EF Core convention configuration.")]
    private sealed class UtcNullableDateTimeValueConverter() : ValueConverter<DateTime?, DateTime?>(
            value => value.HasValue ? (value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime()) : value,
            value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);
}







