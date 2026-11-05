namespace HouseBroker.Domain;

/// <summary>
/// Helpers for the upper-bound band model. Bands are ordered by <see cref="CommissionRate.MaxPrice"/>
/// with the open-ended band last; each band's lower bound is the previous band's upper bound, and the
/// first band starts at zero inclusive.
/// </summary>
public static class CommissionTierSet
{
    /// <summary>Returns the active bands effective at <paramref name="instant"/>, ordered by upper bound.</summary>
    public static IReadOnlyList<CommissionRate> ActiveAt(IEnumerable<CommissionRate> tiers, DateTime instant)
        => Order(tiers.Where(tier => tier.IsActive && tier.EffectiveFrom <= instant));

    /// <summary>Orders bands by upper bound ascending, with the open-ended band last.</summary>
    public static IReadOnlyList<CommissionRate> Order(IEnumerable<CommissionRate> tiers)
        => tiers
            .OrderBy(tier => tier.MaxPrice is null ? 1 : 0)
            .ThenBy(tier => tier.MaxPrice)
            .ToList();

    /// <summary>
    /// Derives the lower bound of the band at <paramref name="index"/> in an ordered set. The first
    /// band starts at zero inclusive; every later band starts at the previous band's upper bound with
    /// the opposite inclusivity.
    /// </summary>
    public static (decimal Lower, bool LowerInclusive) BoundsOf(IReadOnlyList<CommissionRate> ordered, int index)
    {
        ArgumentNullException.ThrowIfNull(ordered);

        if (index <= 0 || index > ordered.Count || ordered[index - 1].MaxPrice is not decimal lower)
        {
            return (0m, true);
        }

        return (lower, !ordered[index - 1].MaxInclusive);
    }

    /// <summary>
    /// Finds the band that prices <paramref name="price"/> at <paramref name="nowUtc"/>, or null when no
    /// active band covers it. Negative prices never match.
    /// </summary>
    public static CommissionRate? FindTier(IEnumerable<CommissionRate> tiers, decimal price, DateTime nowUtc)
    {
        if (price < 0m)
        {
            return null;
        }

        foreach (var tier in ActiveAt(tiers, nowUtc))
        {
            if (tier.MaxPrice is not decimal max)
            {
                return tier;
            }

            if (price < max || (price == max && tier.MaxInclusive))
            {
                return tier;
            }
        }

        return null;
    }
}
