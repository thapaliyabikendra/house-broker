using HouseBroker.Domain;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Tests for band ordering, derived bounds and price lookup.</summary>
public sealed class CommissionTierSetTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Bands are ordered by maximum with the open-ended band last.</summary>
    [Fact]
    public void Order_SortsByMaxWithOpenEndedLast()
    {
        var ten = Band(10_000_000m, true);
        var open = Band(null, false);
        var five = Band(5_000_000m, false);

        var ordered = CommissionTierSet.Order([ten, open, five]);

        Assert.Equal(new[] { five.Id, ten.Id, open.Id }, ordered.Select(band => band.Id));
    }

    /// <summary>Only active bands effective at the instant are returned.</summary>
    [Fact]
    public void ActiveAt_FiltersRetiredAndFutureBands()
    {
        var current = Band(5_000_000m, false);
        var retired = Band(10_000_000m, true);
        retired.Retire();
        var future = Band(null, false, effectiveFrom: Now.AddDays(1));

        var active = CommissionTierSet.ActiveAt([current, retired, future], Now);

        Assert.Same(current, Assert.Single(active));
    }

    /// <summary>The first band starts at zero inclusive.</summary>
    [Fact]
    public void BoundsOf_FirstBandStartsAtZeroInclusive()
    {
        var ordered = CommissionTierSet.Order([Band(5_000_000m, false), Band(null, false)]);

        Assert.Equal((0m, true), CommissionTierSet.BoundsOf(ordered, 0));
    }

    /// <summary>A later band starts at the previous maximum with the opposite inclusivity.</summary>
    [Fact]
    public void BoundsOf_LaterBandUsesPreviousMaxWithOppositeInclusivity()
    {
        var ordered = CommissionTierSet.Order(
        [
            Band(5_000_000m, false),
            Band(10_000_000m, true),
            Band(null, false),
        ]);

        Assert.Equal((5_000_000m, true), CommissionTierSet.BoundsOf(ordered, 1));
        Assert.Equal((10_000_000m, false), CommissionTierSet.BoundsOf(ordered, 2));
    }

    /// <summary>The seeded boundaries select the expected band.</summary>
    [Theory]
    [InlineData(0, 2)]
    [InlineData(4_999_999.99, 2)]
    [InlineData(5_000_000, 1.75)]
    [InlineData(10_000_000, 1.75)]
    [InlineData(10_000_000.01, 1.5)]
    [InlineData(1_000_000_000, 1.5)]
    public void FindTier_SeededBoundaries_ReturnsExpectedRate(decimal price, decimal expectedRate)
    {
        var bands = Seeded();

        var band = CommissionTierSet.FindTier(bands, price, Now);

        Assert.NotNull(band);
        Assert.Equal(expectedRate, band.RatePercent);
    }

    /// <summary>A negative price never matches a band.</summary>
    [Fact]
    public void FindTier_NegativePrice_ReturnsNull()
    {
        Assert.Null(CommissionTierSet.FindTier(Seeded(), -1m, Now));
    }

    /// <summary>Retired and future bands are ignored.</summary>
    [Fact]
    public void FindTier_IgnoresRetiredAndFutureBands()
    {
        var bounded = Band(5_000_000m, false);
        var retiredTop = Band(null, false);
        retiredTop.Retire();
        var futureTop = Band(null, false, effectiveFrom: Now.AddDays(1));

        Assert.Null(CommissionTierSet.FindTier([bounded, retiredTop, futureTop], 6_000_000m, Now));
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
