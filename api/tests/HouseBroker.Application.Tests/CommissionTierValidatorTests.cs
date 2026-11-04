using HouseBroker.Application.Services;
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
}
