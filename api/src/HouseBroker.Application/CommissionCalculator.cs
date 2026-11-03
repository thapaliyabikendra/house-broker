using HouseBroker.Domain;

namespace HouseBroker.Application;

/// <summary>Commission calculation result.</summary>
public sealed record CommissionResult(decimal Amount, decimal RatePercent, Guid RateId);

/// <summary>Pure commission calculator.</summary>
public static class CommissionCalculator
{
    /// <summary>Calculates commission.</summary>
    public static CommissionResult Calculate(decimal price, IEnumerable<CommissionRate> tiers, DateTime nowUtc)
    {
        var tier = tiers.FirstOrDefault(tier => tier.IsActive && tier.EffectiveFrom <= nowUtc && tier.Covers(price));
        if (tier is null)
        {
            throw new Domain.Shared.DomainException(Domain.Shared.ErrorCodes.NotFound, "commission.rate");
        }

        var amount = Math.Round(price * tier.RatePercent / 100m, 2, MidpointRounding.AwayFromZero);
        return new CommissionResult(amount, tier.RatePercent, tier.Id);
    }
}