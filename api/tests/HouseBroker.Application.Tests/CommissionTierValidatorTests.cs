using HouseBroker.Application.Contracts;
using HouseBroker.Application.CommissionRates;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Commission tier validator tests.</summary>
public sealed class CommissionTierValidatorTests
{
    /// <summary>Valid set no errors.</summary>
    [Fact]
    public void Validate_ValidSet_NoErrors()
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var tiers = new[]
        {
            CommissionRate.Create(0m, true, 5000000m, false, 2m, true, DateTime.MinValue),
            CommissionRate.Create(5000000m, true, 10000000m, true, 1.75m, true, DateTime.MinValue),
            CommissionRate.Create(10000000m, false, null, false, 1.5m, true, DateTime.MinValue),
        };

        var errors = CommissionTierValidator.Validate(tiers, now);

        Assert.Empty(errors);
    }

    /// <summary>Gap returns error.</summary>
    [Fact]
    public void Validate_Gap_ReturnsError()
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var tiers = new[]
        {
            CommissionRate.Create(0m, true, 4999999m, false, 2m, true, DateTime.MinValue),
            CommissionRate.Create(5000001m, true, null, false, 1.5m, true, DateTime.MinValue),
        };

        var errors = CommissionTierValidator.Validate(tiers, now);

        Assert.NotEmpty(errors);
    }

    /// <summary>Double inclusive boundary returns error.</summary>
    [Fact]
    public void Validate_DoubleInclusiveBoundary_ReturnsError()
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var tiers = new[]
        {
            CommissionRate.Create(0m, true, 5000000m, true, 2m, true, DateTime.MinValue),
            CommissionRate.Create(5000000m, true, null, false, 1.5m, true, DateTime.MinValue),
        };

        var errors = CommissionTierValidator.Validate(tiers, now);

        Assert.NotEmpty(errors);
    }

    /// <summary>Future-dated invalid sets are detected at their effective instant.</summary>
    [Fact]
    public void Validate_FutureTier_DetectsInvalidFutureSet()
    {
        var now = DateTime.UtcNow;
        var future = now.AddHours(1);
        var tiers = new[]
        {
            CommissionRate.Create(0m, true, null, false, 2m, true, DateTime.MinValue),
            CommissionRate.Create(0m, true, null, false, 99m, true, future),
        };

        var nowErrors = CommissionTierValidator.Validate(tiers, now);
        var futureErrors = CommissionTierValidator.Validate(tiers, future);

        Assert.Empty(nowErrors);
        Assert.NotEmpty(futureErrors);
    }

    /// <summary>A gap created by the proposed tier is reported on that tier's MaxPrice field.</summary>
    [Fact]
    public void ToApplicationError_GapCausedByProposedTier_ReportsMaxPriceField()
    {
        var now = DateTime.UtcNow;
        var proposed = CommissionRate.Create(0m, true, 4999999m, false, 2m, true, DateTime.MinValue);
        var tiers = new[]
        {
            proposed,
            CommissionRate.Create(5000001m, true, null, false, 1.5m, true, DateTime.MinValue),
        };

        var error = CommissionTierValidator.ToApplicationError(CommissionTierValidator.Validate(tiers, now), proposed);

        Assert.Equal(HouseBroker.Application.Contracts.ErrorKind.Validation, error.Kind);
        Assert.Contains(error.FieldErrors, f => f.Field == "MaxPrice");
        Assert.All(error.FieldErrors, f => Assert.False(string.IsNullOrWhiteSpace(f.Code)));
    }

    /// <summary>A problem between two existing tiers does not invent a field on the proposed tier.</summary>
    [Fact]
    public void ToApplicationError_ProblemBetweenExistingTiers_HasNoFieldErrors()
    {
        var now = DateTime.UtcNow;
        var proposed = CommissionRate.Create(20000000m, false, null, false, 1.5m, false, DateTime.MinValue);
        var tiers = new[]
        {
            CommissionRate.Create(0m, true, 4999999m, false, 2m, true, DateTime.MinValue),
            CommissionRate.Create(5000001m, true, 10000000m, true, 1.75m, true, DateTime.MinValue),
            proposed,
        };

        var error = CommissionTierValidator.ToApplicationError(CommissionTierValidator.Validate(tiers, now), proposed);

        Assert.Equal(HouseBroker.Application.Contracts.ErrorKind.Conflict, error.Kind);
        Assert.Equal(ErrorCodes.Conflict, error.Code);
        Assert.Empty(error.FieldErrors);
        Assert.Contains(tiers[0].Id, error.RelatedIds);
        Assert.Contains(tiers[1].Id, error.RelatedIds);
        Assert.DoesNotContain(proposed.Id, error.RelatedIds);
    }
    /// <summary>A boundary mismatch carries both boundaries, seen from each tier's side.</summary>
    [Fact]
    public void Validate_BoundaryMismatch_CarriesBothBoundariesAsArgs()
    {
        var now = DateTime.UtcNow;
        var first = CommissionRate.Create(0m, true, 5000000m, false, 2m, true, DateTime.MinValue);
        var second = CommissionRate.Create(6000000m, true, null, false, 1.5m, true, DateTime.MinValue);

        var errors = CommissionTierValidator.Validate(new[] { first, second }, now);

        var previous = Assert.Single(errors, violation => violation.Code == ErrorCodes.TierBoundaryMismatch && ReferenceEquals(violation.Tier, first));
        Assert.Equal("MaxPrice", previous.Field);
        Assert.Equal(new object?[] { 5000000m, 6000000m }, previous.Args);
        var current = Assert.Single(errors, violation => violation.Code == ErrorCodes.TierBoundaryMismatch && ReferenceEquals(violation.Tier, second));
        Assert.Equal("MinPrice", current.Field);
        Assert.Equal(new object?[] { 6000000m, 5000000m }, current.Args);
    }

    /// <summary>An inclusivity clash carries the shared boundary.</summary>
    [Fact]
    public void Validate_InclusiveMismatch_CarriesSharedBoundary()
    {
        var now = DateTime.UtcNow;
        var tiers = new[]
        {
            CommissionRate.Create(0m, true, 5000000m, true, 2m, true, DateTime.MinValue),
            CommissionRate.Create(5000000m, true, null, false, 1.5m, true, DateTime.MinValue),
        };

        var violation = Assert.Single(CommissionTierValidator.Validate(tiers, now), item => item.Code == ErrorCodes.TierBoundaryInclusiveMismatch && ReferenceEquals(item.Tier, tiers[0]));

        Assert.Equal(new object?[] { 5000000m }, violation.Args);
    }

    /// <summary>The open-ended count violation carries how many open-ended tiers were found.</summary>
    [Fact]
    public void Validate_OpenEndedCount_CarriesFoundCount()
    {
        var now = DateTime.UtcNow;
        var tiers = new[]
        {
            CommissionRate.Create(0m, true, null, false, 2m, true, DateTime.MinValue),
            CommissionRate.Create(5000000m, true, null, false, 1.5m, true, DateTime.MinValue),
        };

        var violation = Assert.Single(CommissionTierValidator.Validate(tiers, now), item => item.Code == ErrorCodes.TierOpenEndedCount);

        Assert.Equal(new object?[] { 2 }, violation.Args);
    }

    /// <summary>A lowest tier that does not start at zero has its own code and carries its minimum.</summary>
    [Fact]
    public void Validate_FirstTierNotZero_UsesSpecificCodeWithMinimum()
    {
        var now = DateTime.UtcNow;
        var tiers = new[] { CommissionRate.Create(100m, true, null, false, 2m, true, DateTime.MinValue) };

        var violation = Assert.Single(CommissionTierValidator.Validate(tiers, now), item => item.Code == ErrorCodes.TierMinNotZero);

        Assert.Equal("MinPrice", violation.Field);
        Assert.Equal(new object?[] { 100m }, violation.Args);
    }

    /// <summary>A maximum at or below the minimum has its own code and carries both values.</summary>
    [Fact]
    public void Validate_MaxNotAboveMin_UsesSpecificCodeWithBothValues()
    {
        var now = DateTime.UtcNow;
        var tiers = new[] { CommissionRate.Create(0m, true, 0m, false, 2m, true, DateTime.MinValue) };

        var violation = Assert.Single(CommissionTierValidator.Validate(tiers, now), item => item.Code == ErrorCodes.TierMaxNotAboveMin);

        Assert.Equal("MaxPrice", violation.Field);
        Assert.Equal(new object?[] { 0m, 0m }, violation.Args);
    }

    /// <summary>A set with no active tier reports the set-level code, not the generic required code.</summary>
    [Fact]
    public void Validate_NoActiveTiers_ReportsTierSetEmpty()
    {
        var now = DateTime.UtcNow;
        var tiers = new[] { CommissionRate.Create(0m, true, null, false, 2m, false, DateTime.MinValue) };

        var violation = Assert.Single(CommissionTierValidator.Validate(tiers, now));

        Assert.Equal(ErrorCodes.TierSetEmpty, violation.Code);
        Assert.Null(violation.Tier);
    }

    /// <summary>Field errors on the proposed tier keep their args, and the set-level error uses the rule's own code.</summary>
    [Fact]
    public void ToApplicationError_GapCausedByProposedTier_FieldErrorCarriesArgs()
    {
        var now = DateTime.UtcNow;
        var proposed = CommissionRate.Create(0m, true, 4999999m, false, 2m, true, DateTime.MinValue);
        var tiers = new[]
        {
            proposed,
            CommissionRate.Create(5000001m, true, null, false, 1.5m, true, DateTime.MinValue),
        };

        var error = CommissionTierValidator.ToApplicationError(CommissionTierValidator.Validate(tiers, now), proposed);

        var fieldError = Assert.Single(error.FieldErrors);
        Assert.Equal("MaxPrice", fieldError.Field);
        Assert.Equal(ErrorCodes.TierBoundaryMismatch, fieldError.Code);
        Assert.Equal(new object?[] { 4999999m, 5000001m }, fieldError.Args);
    }

    /// <summary>A set-level failure with no field and no related tiers uses the rule's own code.</summary>
    [Fact]
    public void ToApplicationError_NoActiveTiers_UsesSetLevelCode()
    {
        var now = DateTime.UtcNow;
        var proposed = CommissionRate.Create(0m, true, null, false, 2m, false, DateTime.MinValue);

        var error = CommissionTierValidator.ToApplicationError(CommissionTierValidator.Validate(new[] { proposed }, now), proposed);

        Assert.Equal(ErrorKind.Validation, error.Kind);
        Assert.Equal(ErrorCodes.TierSetEmpty, error.Code);
    }

    /// <summary>Existing tiers that break the rules are reported with their range as arguments.</summary>
    [Fact]
    public void ToApplicationError_ExistingBrokenTiers_CarryRangeInRelatedRecords()
    {
        var now = DateTime.UtcNow;
        var proposed = CommissionRate.Create(20000000m, false, null, false, 1.5m, false, DateTime.MinValue);
        var tiers = new[]
        {
            CommissionRate.Create(0m, true, 4999999m, false, 2m, true, DateTime.MinValue),
            CommissionRate.Create(5000001m, true, 10000000m, true, 1.75m, true, DateTime.MinValue),
            proposed,
        };

        var error = CommissionTierValidator.ToApplicationError(CommissionTierValidator.Validate(tiers, now), proposed);

        var first = Assert.Single(error.RelatedRecords, record => record.Id == tiers[0].Id);
        Assert.Equal(new object?[] { 0m, true, 4999999m, false }, first.Args);
        var second = Assert.Single(error.RelatedRecords, record => record.Id == tiers[1].Id);
        Assert.Equal(new object?[] { 5000001m, true, 10000000m, true }, second.Args);
    }
}
