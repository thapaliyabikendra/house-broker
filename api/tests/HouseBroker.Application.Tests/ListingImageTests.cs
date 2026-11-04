using HouseBroker.Domain;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Listing primary-image invariant tests.</summary>
public sealed class ListingImageTests
{
    /// <summary>The first uploaded image becomes the primary image at sort order zero.</summary>
    [Fact]
    public void AddImage_FirstImage_BecomesPrimaryAndSortOrderZero()
    {
        var listing = CreateListing();
        var image = CreateImage(listing.Id, isPrimary: false);

        listing.AddImage(image);

        Assert.True(image.IsPrimary);
        Assert.Equal(0, image.SortOrder);
    }

    /// <summary>Later uploads receive ascending sort orders and are not primary.</summary>
    [Fact]
    public void AddImage_LaterImages_GetAscendingSortOrder()
    {
        var listing = CreateListing();
        var first = CreateImage(listing.Id, isPrimary: false);
        var second = CreateImage(listing.Id, isPrimary: false);
        var third = CreateImage(listing.Id, isPrimary: false);

        listing.AddImage(first);
        listing.AddImage(second);
        listing.AddImage(third);

        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);
        Assert.False(third.IsPrimary);
        Assert.Equal(0, first.SortOrder);
        Assert.Equal(1, second.SortOrder);
        Assert.Equal(2, third.SortOrder);
    }

    /// <summary>Adding an explicit primary clears the previous primary.</summary>
    [Fact]
    public void AddImage_ExplicitPrimary_ClearsPreviousPrimary()
    {
        var listing = CreateListing();
        var first = CreateImage(listing.Id, isPrimary: false);
        var second = CreateImage(listing.Id, isPrimary: true);

        listing.AddImage(first);
        listing.AddImage(second);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
    }

    /// <summary>Removing the primary promotes the remaining image with the lowest sort order.</summary>
    [Fact]
    public void RemoveImage_Primary_PromotesLowestSortOrder()
    {
        var listing = CreateListing();
        var first = CreateImage(listing.Id, isPrimary: false);
        var second = CreateImage(listing.Id, isPrimary: false);
        var third = CreateImage(listing.Id, isPrimary: false);
        listing.AddImage(first);
        listing.AddImage(second);
        listing.AddImage(third);

        listing.RemoveImage(first.Id);

        Assert.True(second.IsPrimary);
        Assert.False(third.IsPrimary);
    }

    /// <summary>Removing a non-primary image leaves the primary unchanged.</summary>
    [Fact]
    public void RemoveImage_NonPrimary_KeepsPrimary()
    {
        var listing = CreateListing();
        var first = CreateImage(listing.Id, isPrimary: false);
        var second = CreateImage(listing.Id, isPrimary: false);
        listing.AddImage(first);
        listing.AddImage(second);

        listing.RemoveImage(second.Id);

        Assert.True(first.IsPrimary);
    }

    /// <summary>Removing the only image leaves no primary behind.</summary>
    [Fact]
    public void RemoveImage_LastImage_LeavesNoPrimary()
    {
        var listing = CreateListing();
        var only = CreateImage(listing.Id, isPrimary: false);
        listing.AddImage(only);

        listing.RemoveImage(only.Id);

        Assert.Empty(listing.Images);
    }

    private static Listing CreateListing() => Listing.Create(
        Guid.NewGuid(),
        "Title",
        "Description",
        Guid.NewGuid(),
        100m,
        "P",
        "C",
        "L",
        "12345",
        1,
        1,
        50m);

    private static ListingImage CreateImage(Guid listingId, bool isPrimary)
        => ListingImage.Create(listingId, "file.jpg", "image/jpeg", 10, "hash", isPrimary);
}
