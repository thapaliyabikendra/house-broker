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

    /// <summary>Each rule reports a specific code: missing values are required, out-of-range values are out_of_range.</summary>
    [Fact]
    public async Task CreateValidator_ReportsSpecificCodesPerRule()
    {
        var validator = new CreateListingRequestValidator();

        var request = new CreateListingRequest
        {
            Title = string.Empty,
            PropertyTypeId = Guid.NewGuid(),
            Price = -1m,
            Province = "P",
            City = "C",
            Locality = "L",
            PostalCode = "12345",
            Bedrooms = 99,
        };

        var result = await validator.ValidateAsync(request);

        Assert.Contains(result.Errors, f => f.PropertyName == "Title" && f.ErrorCode == "validation.required");
        Assert.Contains(result.Errors, f => f.PropertyName == "Price" && f.ErrorCode == "validation.out_of_range");
        Assert.Contains(result.Errors, f => f.PropertyName == "Bedrooms" && f.ErrorCode == "validation.out_of_range");
    }

    /// <summary>HTML in free text and a malformed postal code get their own codes, not the generic one.</summary>
    [Fact]
    public async Task CreateValidator_ReportsHtmlAndFormatCodes()
    {
        var validator = new CreateListingRequestValidator();

        var request = new CreateListingRequest
        {
            Title = "<b>Bold</b>",
            PropertyTypeId = Guid.NewGuid(),
            Price = 100m,
            Province = "P",
            City = "C",
            Locality = "L",
            PostalCode = "123",
            Description = "<script>x</script>",
        };

        var result = await validator.ValidateAsync(request);

        Assert.Contains(result.Errors, f => f.PropertyName == "Title" && f.ErrorCode == "validation.html_not_allowed");
        Assert.Contains(result.Errors, f => f.PropertyName == "Description" && f.ErrorCode == "validation.html_not_allowed");
        Assert.Contains(result.Errors, f => f.PropertyName == "PostalCode" && f.ErrorCode == "validation.invalid_format");
        Assert.DoesNotContain(result.Errors, f => f.ErrorCode == "validation.failed");
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
