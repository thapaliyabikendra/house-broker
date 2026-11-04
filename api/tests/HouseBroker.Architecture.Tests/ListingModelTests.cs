using HouseBroker.Domain;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HouseBroker.Architecture.Tests;

/// <summary>Entity Framework model invariants for listing relationships.</summary>
public sealed class ListingModelTests
{
    /// <summary>The model does not create shadow listing foreign keys.</summary>
    [Fact]
    public void ListingRelationships_DoNotUseShadowForeignKeys()
    {
        using var context = CreateContext();
        var listingFeature = context.Model.FindEntityType(typeof(ListingFeature));
        var listingImage = context.Model.FindEntityType(typeof(ListingImage));
        var listing = context.Model.FindEntityType(typeof(Listing));

        Assert.NotNull(listingFeature);
        Assert.NotNull(listingImage);
        Assert.NotNull(listing);
        Assert.DoesNotContain(listingFeature.GetProperties(), property => property.Name == "ListingId1");
        Assert.DoesNotContain(listingImage.GetProperties(), property => property.Name == "ListingId1");

        var featureForeignKey = Assert.Single(
            listingFeature.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Any(property => property.Name == nameof(ListingFeature.ListingId)));
        Assert.Equal(typeof(Listing), featureForeignKey.PrincipalEntityType.ClrType);
        Assert.False(featureForeignKey.IsUnique);

        var imageForeignKey = Assert.Single(
            listingImage.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Any(property => property.Name == nameof(ListingImage.ListingId)));
        Assert.Equal(typeof(Listing), imageForeignKey.PrincipalEntityType.ClrType);
        Assert.False(imageForeignKey.IsUnique);

        var featureNavigation = listing.FindNavigation(nameof(Listing.Features));
        Assert.NotNull(featureNavigation);
        Assert.Equal(typeof(ListingFeature), featureNavigation.TargetEntityType.ClrType);

        var imageNavigation = listing.FindNavigation(nameof(Listing.Images));
        Assert.NotNull(imageNavigation);
        Assert.Equal(typeof(ListingImage), imageNavigation.TargetEntityType.ClrType);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=HouseBrokerModelTests;Trusted_Connection=True")
            .Options;
        return new AppDbContext(options);
    }
}
