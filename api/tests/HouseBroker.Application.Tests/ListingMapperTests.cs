using HouseBroker.Application.Contracts.Listings;
using HouseBroker.Application.Mapping;

using HouseBroker.TestSupport;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Mapperly listing mapping tests.</summary>
public sealed class ListingMapperTests
{
    /// <summary>Public detail does not contain commission.</summary>
    [Fact]
    public void ToDetail_DoesNotContainCommission()
    {
        var listing = ListingBuilder.Create(price: 100m);

        var dto = ListingMapper.Instance.ToDetail(listing);

        Assert.Equal(listing.Id, dto.Id);
        Assert.DoesNotContain(typeof(ListingDetailDto).GetProperties(), property => property.Name.StartsWith("Commission", StringComparison.Ordinal));
    }

    /// <summary>Owner mapping contains commission.</summary>
    [Fact]
    public void ToOwner_ContainsCommissionProperties()
    {
        var listing = ListingBuilder.Create(price: 100m);
        listing.ApplyCommission(2m, 2m, Guid.NewGuid());

        var dto = ListingMapper.Instance.ToOwner(listing);

        Assert.Equal(listing.CommissionAmount, dto.CommissionAmount);
        Assert.Equal(listing.CommissionRatePercent, dto.CommissionRatePercent);
        Assert.Equal(listing.CommissionRateId, dto.CommissionRateId);
    }
}
