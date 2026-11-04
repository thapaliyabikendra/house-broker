using HouseBroker.Application.Contracts;
using HouseBroker.Domain;

namespace HouseBroker.TestSupport;

/// <summary>Helpers for creating test listings.</summary>
public static class ListingBuilder
{
    /// <summary>Creates a listing with sensible defaults.</summary>
    public static Listing Create(
        Guid? brokerId = null,
        string title = "Test listing",
        decimal price = 100m,
        int bedrooms = 1,
        int bathrooms = 1,
        decimal areaSqFt = 50m) =>
        Listing.Create(
            brokerId ?? Guid.NewGuid(),
            title,
            "Description",
            Guid.NewGuid(),
            price,
            "Province",
            "City",
            "Locality",
            "12345",
            bedrooms,
            bathrooms,
            areaSqFt);
}

/// <summary>Helpers for creating test listing requests.</summary>
public static class ListingRequestBuilder
{
    /// <summary>Creates a valid create request.</summary>
    public static CreateListingRequest CreateValidCreateRequest(
        string title = "Title",
        decimal price = 100m) =>
        new()
        {
            Title = title,
            PropertyTypeId = Guid.NewGuid(),
            Price = price,
            Province = "P",
            City = "C",
            Locality = "L",
            PostalCode = "12345",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaSqFt = 50m,
        };

    /// <summary>Creates a valid update request.</summary>
    public static UpdateListingRequest CreateValidUpdateRequest(
        string title = "Title",
        decimal price = 100m) =>
        new()
        {
            Title = title,
            PropertyTypeId = Guid.NewGuid(),
            Price = price,
            Province = "P",
            City = "C",
            Locality = "L",
            PostalCode = "12345",
            Bedrooms = 1,
            Bathrooms = 1,
            AreaSqFt = 50m,
        };
}
