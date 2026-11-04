using HouseBroker.Application.Services;
using HouseBroker.Domain;
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
}

