using HouseBroker.Application.CommissionRates;
using HouseBroker.Domain;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Commission calculator tests.</summary>
public sealed class CommissionCalculatorTests
{
    /// <summary>Calculates expected rate.</summary>
    [Theory]
    [InlineData(4999999.50, 2)]
    [InlineData(5000000.00, 1.75)]
    [InlineData(10000000.00, 1.75)]
    [InlineData(10000000.01, 1.5)]
    public void Calculate_ReturnsExpectedTier(decimal price, decimal expectedRate)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var tiers = new[]
        {
            CommissionRate.Create(0m, true, 5000000m, false, 2m, true, DateTime.MinValue),
            CommissionRate.Create(5000000m, true, 10000000m, true, 1.75m, true, DateTime.MinValue),
            CommissionRate.Create(10000000m, false, null, false, 1.5m, true, DateTime.MinValue),
        };

        var result = CommissionCalculator.Calculate(price, tiers, now);

        Assert.Equal(expectedRate, result.RatePercent);
        Assert.Equal(Math.Round(price * expectedRate / 100m, 2, MidpointRounding.AwayFromZero), result.Amount);
    }

    /// <summary>Ignores future tiers.</summary>
    [Fact]
    public void Calculate_IgnoresFutureTier()
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var tiers = new[]
        {
            CommissionRate.Create(0m, true, null, false, 2m, true, DateTime.MinValue),
            CommissionRate.Create(0m, true, null, false, 99m, true, now.AddMinutes(5)),
        };

        var result = CommissionCalculator.Calculate(1000m, tiers, now);

        Assert.Equal(2m, result.RatePercent);
    }

    /// <summary>Throws when no tier.</summary>
    [Fact]
    public void Calculate_NoTier_Throws()
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var tiers = Array.Empty<CommissionRate>();

        Assert.Throws<HouseBroker.Domain.Common.DomainException>(() => CommissionCalculator.Calculate(1000m, tiers, now));
    }
}


