using HouseBroker.Application.Contracts;
using HouseBroker.Application.Services;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Listing validators tests.</summary>
public sealed class ListingValidatorsTests
{
    /// <summary>Create validator rejects HTML and bad postal code.</summary>
    [Fact]
    public async Task CreateValidator_RejectsInvalid()
    {
        var validator = new CreateListingRequestValidator();

        var request = new CreateListingRequest
        {
            Title = "<b>x</b>",
            PropertyTypeId = Guid.NewGuid(),
            Price = 100m,
            Province = "P",
            City = "C",
            Locality = "L",
            PostalCode = "123",
        };

        var result = await validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }

    /// <summary>Create validator accepts a valid request.</summary>
    [Fact]
    public async Task CreateValidator_AcceptsValid()
    {
        var validator = new CreateListingRequestValidator();

        var request = new CreateListingRequest
        {
            Title = "Valid title",
            PropertyTypeId = Guid.NewGuid(),
            Price = 100m,
            Province = "P",
            City = "C",
            Locality = "L",
            PostalCode = "12345",
            Bedrooms = 2,
            Bathrooms = 1,
            AreaSqFt = 50m,
        };

        var result = await validator.ValidateAsync(request);

        Assert.True(result.IsValid);
    }
}
