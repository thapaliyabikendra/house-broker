using HouseBroker.Application.Contracts;
using HouseBroker.Application.CommissionRates;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Commission band validator tests for the upper-bound model.</summary>
public sealed class CommissionTierValidatorTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The seeded upper-bound bands are valid.</summary>
    [Fact]
    public void Validate_ValidSeededSet_NoViolations()
    {
        var bands = new[]
        {
            Band(5_000_000m, false, 2m),
            Band(10_000_000m, true, 1.75m),
            Band(null, false, 1.5m),
        };

        Assert.Empty(CommissionTierValidator.Validate(bands, Now));
    }

    /// <summary>Two bounded bands with the same maximum break strict ordering.</summary>
    [Fact]
    public void Validate_DuplicateMax_ReturnsTierMaxNotAscending()
    {
        var first = Band(5_000_000m, false);
        var second = Band(5_000_000m, true);
        var bands = new[] { first, second, Band(null, false) };

        var violation = Assert.Single(CommissionTierValidator.Validate(bands, Now), item => item.Code == ErrorCodes.TierMaxNotAscending);

        Assert.Same(second, violation.Tier);
        Assert.Equal("MaxPrice", violation.Field);
        Assert.Equal(new object?[] { 5_000_000m, 5_000_000m }, violation.Args);
    }

    /// <summary>Input order does not matter: the set is ordered by maximum before validation.</summary>
    [Fact]
    public void Validate_DescendingInput_IsNormalizedToAscendingOrder()
    {
        var bands = new[]
        {
            Band(10_000_000m, false),
            Band(5_000_000m, false),
            Band(null, false),
        };

        Assert.Empty(CommissionTierValidator.Validate(bands, Now));
    }

    /// <summary>A bounded maximum of zero is rejected; the first band would be empty.</summary>
    [Fact]
    public void Validate_ZeroMax_ReturnsTierMaxNotAboveMin()
    {
        var zero = WithMax(Band(null, false), 0m);
        var bands = new[] { zero, Band(null, false) };

        var violation = Assert.Single(CommissionTierValidator.Validate(bands, Now), item => item.Code == ErrorCodes.TierMaxNotAboveMin);

        Assert.Equal("MaxPrice", violation.Field);
        Assert.Equal(new object?[] { 0m, 0m }, violation.Args);
    }

    /// <summary>A negative bounded maximum is rejected.</summary>
    [Fact]
    public void Validate_NegativeMax_ReturnsTierMaxNotAboveMin()
    {
        var negative = WithMax(Band(null, false), -1m);
        var bands = new[] { negative, Band(null, false) };

        var violation = Assert.Single(CommissionTierValidator.Validate(bands, Now), item => item.Code == ErrorCodes.TierMaxNotAboveMin);

        Assert.Equal(new object?[] { 0m, -1m }, violation.Args);
    }

    /// <summary>Every active set needs exactly one open-ended top band.</summary>
    [Fact]
    public void Validate_NoOpenEnded_ReturnsTierOpenEndedCount()
    {
        var bands = new[] { Band(5_000_000m, false), Band(10_000_000m, true) };

        var violation = Assert.Single(CommissionTierValidator.Validate(bands, Now), item => item.Code == ErrorCodes.TierOpenEndedCount);

        Assert.Equal(new object?[] { 0 }, violation.Args);
    }

    /// <summary>Two open-ended bands are rejected and the found count is reported.</summary>
    [Fact]
    public void Validate_TwoOpenEnded_ReturnsTierOpenEndedCount()
    {
        var bands = new[] { Band(null, false), Band(null, false) };

        var violation = Assert.Single(CommissionTierValidator.Validate(bands, Now), item => item.Code == ErrorCodes.TierOpenEndedCount);

        Assert.Equal(new object?[] { 2 }, violation.Args);
    }

    /// <summary>A rate outside (0, 100] is rejected with the offending value.</summary>
    [Fact]
    public void Validate_RateOutOfRange_ReturnsTierRateOutOfRange()
    {
        var invalid = WithRate(Band(5_000_000m, false), 0m);
        var bands = new[] { invalid, Band(null, false) };

        var violation = Assert.Single(CommissionTierValidator.Validate(bands, Now), item => item.Code == ErrorCodes.TierRateOutOfRange);

        Assert.Equal("RatePercent", violation.Field);
        Assert.Equal(new object?[] { 0m }, violation.Args);
    }

    /// <summary>A future band that would break the set is detected at its effective instant.</summary>
    [Fact]
    public void ValidateTimeline_FutureTierBreakingSet_IsRejected()
    {
        var future = Now.AddHours(1);
        var bands = new[]
        {
            Band(null, false, 2m),
            Band(null, false, 99m, future),
        };

        Assert.Empty(CommissionTierValidator.Validate(bands, Now));
        Assert.NotEmpty(CommissionTierValidator.Validate(bands, future));
        Assert.NotEmpty(CommissionTierValidator.ValidateTimeline(bands, Now));
    }

    /// <summary>A duplicate maximum caused by the proposed band is reported on that band's MaxPrice field.</summary>
    [Fact]
    public void ToApplicationError_DuplicateMaxCausedByProposed_ReportsMaxPriceField()
    {
        var existing = Band(5_000_000m, false);
        var proposed = Band(5_000_000m, true);
        var bands = new[] { existing, proposed, Band(null, false) };

        var error = CommissionTierValidator.ToApplicationError(
            CommissionTierValidator.Validate(bands, Now),
            proposed,
            storedSetInvalid: false);

        Assert.Equal(ErrorKind.Validation, error.Kind);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
        var fieldError = Assert.Single(error.FieldErrors);
        Assert.Equal("MaxPrice", fieldError.Field);
        Assert.Equal(ErrorCodes.TierMaxNotAscending, fieldError.Code);
        Assert.Equal(new object?[] { 5_000_000m, 5_000_000m }, fieldError.Args);
        Assert.Contains(existing.Id, error.RelatedIds);
        Assert.DoesNotContain(proposed.Id, error.RelatedIds);
    }

    /// <summary>A problem between stored bands is a state conflict, not a bad request field.</summary>
    [Fact]
    public void ToApplicationError_ProblemBetweenExistingBands_IsConflict()
    {
        var first = Band(5_000_000m, false);
        var second = Band(5_000_000m, true);
        var proposed = Band(null, false);
        var bands = new[] { first, second, proposed };

        var error = CommissionTierValidator.ToApplicationError(
            CommissionTierValidator.Validate(bands, Now),
            proposed,
            storedSetInvalid: true);

        Assert.Equal(ErrorKind.Conflict, error.Kind);
        Assert.Equal(ErrorCodes.Conflict, error.Code);
        Assert.Empty(error.FieldErrors);
        Assert.Contains(second.Id, error.RelatedIds);
        Assert.DoesNotContain(proposed.Id, error.RelatedIds);
    }

    /// <summary>Related bands are described by their upper bound only.</summary>
    [Fact]
    public void ToApplicationError_ExistingBrokenBands_CarryMaxOnlyRelatedRecords()
    {
        var first = Band(5_000_000m, false);
        var second = Band(5_000_000m, true);
        var proposed = Band(null, false);
        var bands = new[] { first, second, proposed };

        var error = CommissionTierValidator.ToApplicationError(
            CommissionTierValidator.Validate(bands, Now),
            proposed,
            storedSetInvalid: true);

        var records = error.RelatedRecords.OrderBy(record => record.Id == first.Id ? 0 : 1).ToList();
        Assert.Equal(2, records.Count);
        Assert.Equal(first.Id, records[0].Id);
        Assert.Equal(new object?[] { 5_000_000m, false }, records[0].Args);
        Assert.Equal(second.Id, records[1].Id);
        Assert.Equal(new object?[] { 5_000_000m, true }, records[1].Args);
    }

    /// <summary>A set-level failure with nothing to point at keeps its own rule code.</summary>
    [Fact]
    public void ToApplicationError_NoActiveBands_UsesSetLevelCode()
    {
        var proposed = Band(null, false);
        proposed.Retire();

        var error = CommissionTierValidator.ToApplicationError(
            CommissionTierValidator.Validate(new[] { proposed }, Now),
            proposed,
            storedSetInvalid: false);

        Assert.Equal(ErrorKind.Validation, error.Kind);
        Assert.Equal(ErrorCodes.TierSetEmpty, error.Code);
        Assert.Empty(error.FieldErrors);
    }

    /// <summary>Lifecycle failures map to a conflict with the offending bands attached.</summary>
    [Fact]
    public void ToConflict_AttachesRelatedBands()
    {
        var bands = new[] { Band(5_000_000m, false), Band(10_000_000m, true) };

        var error = CommissionTierValidator.ToConflict(CommissionTierValidator.Validate(bands, Now));

        Assert.Equal(ErrorKind.Conflict, error.Kind);
        Assert.Equal(ErrorCodes.TierOpenEndedCount, error.Code);
        var related = Assert.Single(error.RelatedIds);
        Assert.Equal(bands[1].Id, related);
    }

    private static CommissionRate Band(decimal? maxPrice, bool maxInclusive, decimal ratePercent = 2m, DateTime? effectiveFrom = null)
        => CommissionRate.Create(maxPrice, maxInclusive, ratePercent, effectiveFrom ?? DateTime.MinValue);

    private static CommissionRate WithMax(CommissionRate band, decimal? maxPrice, bool maxInclusive = false)
    {
        Set(band, nameof(CommissionRate.MaxPrice), maxPrice);
        Set(band, nameof(CommissionRate.MaxInclusive), maxInclusive);
        return band;
    }

    private static CommissionRate WithRate(CommissionRate band, decimal ratePercent)
    {
        Set(band, nameof(CommissionRate.RatePercent), ratePercent);
        return band;
    }

    private static void Set(CommissionRate band, string property, object? value)
        => typeof(CommissionRate)
            .GetProperty(property)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(band, [value]);
}
