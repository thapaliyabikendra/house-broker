using HouseBroker.Application.CommissionRates;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Commission calculator tests for the upper-bound band model.</summary>
public sealed class CommissionCalculatorTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Calculates the expected rate at and around every seeded boundary.</summary>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(4_999_999, 2)]
    [InlineData(4_999_999.50, 2)]
    [InlineData(5_000_000, 1.75)]
    [InlineData(10_000_000, 1.75)]
    [InlineData(10_000_000.01, 1.5)]
    public void Calculate_ReturnsExpectedBand(decimal price, decimal expectedRate)
    {
        var result = CommissionCalculator.Calculate(price, Seeded(), Now);

        Assert.Equal(expectedRate, result.RatePercent);
        Assert.Equal(Math.Round(price * expectedRate / 100m, 2, MidpointRounding.AwayFromZero), result.Amount);
    }

    /// <summary>Ignores a future band.</summary>
    [Fact]
    public void Calculate_IgnoresFutureBand()
    {
        var bands = new[]
        {
            Band(null, false, 2m),
            Band(null, false, 99m, Now.AddMinutes(5)),
        };

        var result = CommissionCalculator.Calculate(1_000m, bands, Now);

        Assert.Equal(2m, result.RatePercent);
    }

    /// <summary>Ignores a retired band.</summary>
    [Fact]
    public void Calculate_IgnoresRetiredBand()
    {
        var retired = Band(null, false, 99m);
        retired.Retire();
        var bands = new[] { Band(5_000_000m, false, 2m), retired };

        Assert.Throws<DomainException>(() => CommissionCalculator.Calculate(6_000_000m, bands, Now));
    }

    /// <summary>A negative price never matches a band.</summary>
    [Fact]
    public void Calculate_NegativePrice_Throws()
    {
        Assert.Throws<DomainException>(() => CommissionCalculator.Calculate(-1m, Seeded(), Now));
    }

    /// <summary>Throws when no band covers the price.</summary>
    [Fact]
    public void Calculate_NoBand_Throws()
    {
        Assert.Throws<DomainException>(() => CommissionCalculator.Calculate(1_000m, [], Now));
    }

    private static CommissionRate[] Seeded() =>
    [
        Band(5_000_000m, false, 2m),
        Band(10_000_000m, true, 1.75m),
        Band(null, false, 1.5m),
    ];

    private static CommissionRate Band(decimal? maxPrice, bool maxInclusive, decimal ratePercent = 2m, DateTime? effectiveFrom = null)
        => CommissionRate.Create(maxPrice, maxInclusive, ratePercent, effectiveFrom ?? DateTime.MinValue);
}
