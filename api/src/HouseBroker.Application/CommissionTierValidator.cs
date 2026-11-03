using HouseBroker.Domain;

namespace HouseBroker.Application.Services;

/// <summary>Commission tier validation.</summary>
public static class CommissionTierValidator
{
    /// <summary>Validates a set of commission tiers at the supplied instant.</summary>
    public static IReadOnlyList<string> Validate(IEnumerable<CommissionRate> tiers, DateTime effectiveAtUtc)
    {
        var errors = new List<string>();
        var active = tiers
            .Where(tier => tier.IsActive && tier.EffectiveFrom <= effectiveAtUtc)
            .OrderBy(tier => tier.MinPrice)
            .ThenBy(tier => tier.MinInclusive ? 0 : 1)
            .ToList();

        if (active.Count == 0)
        {
            errors.Add("At least one active tier is required.");
            return errors;
        }

        if (active.Count(tier => tier.MaxPrice is null) != 1)
        {
            errors.Add("Exactly one open-ended top tier is required.");
        }

        var first = active[0];
        if (first.MinPrice != 0m || !first.MinInclusive)
        {
            errors.Add("The first tier must start at zero and be inclusive.");
        }

        foreach (var tier in active)
        {
            if (tier.MaxPrice is decimal max)
            {
                if (max <= tier.MinPrice)
                {
                    errors.Add("A tier maximum must be greater than its minimum.");
                }
            }
        }

        if (active.Count(tier => tier.MaxPrice is null) > 0 && active[^1].MaxPrice is not null)
        {
            errors.Add("The open-ended tier must be the final tier.");
        }

        for (var index = 1; index < active.Count; index++)
        {
            var previous = active[index - 1];
            var current = active[index];

            if (previous.MaxPrice is null)
            {
                errors.Add("Only the final tier may be open-ended.");
                continue;
            }

            if (previous.MaxPrice.Value != current.MinPrice)
            {
                errors.Add("Adjacent tiers must share exactly one boundary value.");
                continue;
            }

            if (previous.MaxInclusive == current.MinInclusive)
            {
                errors.Add("Exactly one side of a shared boundary may be inclusive.");
            }
        }

        return errors;
    }
}
