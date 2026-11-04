using HouseBroker.Application.Contracts;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;

namespace HouseBroker.Application.CommissionRates;

/// <summary>
/// One broken rule in a commission tier set, with the tier and request field it concerns.
/// Its arguments are the raw values the rule's message needs (boundaries, counts); the API boundary formats them.
/// </summary>
public sealed record CommissionTierViolation
{
    /// <summary>Initializes a violation.</summary>
    public CommissionTierViolation(string code, CommissionRate? tier, string? field, params object?[] args)
    {
        Code = code;
        Tier = tier;
        Field = field;
        Args = args;
    }

    /// <summary>Gets the stable error code.</summary>
    public string Code { get; init; }

    /// <summary>Gets the tier the rule was checked on, or null when the rule is about the set as a whole.</summary>
    public CommissionRate? Tier { get; init; }

    /// <summary>Gets the tier property at fault, or null when no single property is at fault.</summary>
    public string? Field { get; init; }

    /// <summary>Gets the message arguments in placeholder order.</summary>
    public IReadOnlyList<object?> Args { get; init; }
}

/// <summary>Commission tier validation.</summary>
public static class CommissionTierValidator
{
    /// <summary>Validates a set of commission tiers at the supplied instant.</summary>
    public static IReadOnlyList<CommissionTierViolation> Validate(IEnumerable<CommissionRate> tiers, DateTime effectiveAtUtc)
    {
        var violations = new List<CommissionTierViolation>();
        var active = tiers
            .Where(tier => tier.IsActive && tier.EffectiveFrom <= effectiveAtUtc)
            .OrderBy(tier => tier.MinPrice)
            .ThenBy(tier => tier.MinInclusive ? 0 : 1)
            .ToList();

        if (active.Count == 0)
        {
            violations.Add(new CommissionTierViolation(ErrorCodes.TierSetEmpty, null, null));
            return violations;
        }

        var openEnded = active.Where(tier => tier.MaxPrice is null).ToList();
        if (openEnded.Count != 1)
        {
            // Point at the open-ended candidate when there is one; otherwise at the top tier.
            var subject = openEnded.Count > 1 ? openEnded[0] : active[^1];
            violations.Add(new CommissionTierViolation(ErrorCodes.TierOpenEndedCount, subject, "MaxPrice", openEnded.Count));
        }

        var first = active[0];
        if (first.MinPrice != 0m)
        {
            violations.Add(new CommissionTierViolation(ErrorCodes.TierMinNotZero, first, "MinPrice", first.MinPrice));
        }
        else if (!first.MinInclusive)
        {
            violations.Add(new CommissionTierViolation(ErrorCodes.TierLowestInclusive, first, "MinInclusive", first.MinPrice));
        }

        foreach (var tier in active)
        {
            if (tier.MaxPrice is decimal max && max <= tier.MinPrice)
            {
                violations.Add(new CommissionTierViolation(ErrorCodes.TierMaxNotAboveMin, tier, "MaxPrice", tier.MinPrice, max));
            }
        }

        if (openEnded.Count > 0 && active[^1].MaxPrice is not null)
        {
            violations.Add(new CommissionTierViolation(ErrorCodes.TierTopOpenEnded, openEnded[0], "MaxPrice", openEnded[0].MinPrice));
        }

        for (var index = 1; index < active.Count; index++)
        {
            var previous = active[index - 1];
            var current = active[index];

            if (previous.MaxPrice is null)
            {
                violations.Add(new CommissionTierViolation(ErrorCodes.TierOpenEndedLast, previous, "MaxPrice", previous.MinPrice));
                continue;
            }

            // Each side's args are phrased from that tier's point of view: "this boundary" and "the adjacent boundary".
            var sharedBoundary = previous.MaxPrice.Value;
            if (sharedBoundary != current.MinPrice)
            {
                violations.Add(new CommissionTierViolation(ErrorCodes.TierBoundaryMismatch, previous, "MaxPrice", sharedBoundary, current.MinPrice));
                violations.Add(new CommissionTierViolation(ErrorCodes.TierBoundaryMismatch, current, "MinPrice", current.MinPrice, sharedBoundary));
                continue;
            }

            if (previous.MaxInclusive == current.MinInclusive)
            {
                violations.Add(new CommissionTierViolation(ErrorCodes.TierBoundaryInclusiveMismatch, previous, "MaxInclusive", sharedBoundary));
                violations.Add(new CommissionTierViolation(ErrorCodes.TierBoundaryInclusiveMismatch, current, "MinInclusive", sharedBoundary));
            }
        }

        return violations;
    }

    /// <summary>
    /// Builds the validation error for an admin write. Violations on the proposed tier become per-field entries;
    /// violations that only involve existing tiers cannot be fixed by fields of this request, so they produce a
    /// field-less conflict that names those tiers in <see cref="ApplicationError.RelatedRecords"/>.
    /// </summary>
    /// <param name="violations">The violations found by <see cref="Validate"/>.</param>
    /// <param name="proposed">The tier being written.</param>
    public static ApplicationError ToApplicationError(IEnumerable<CommissionTierViolation> violations, CommissionRate proposed)
    {
        var all = violations.ToList();
        var fieldErrors = all
            .Where(violation => ReferenceEquals(violation.Tier, proposed) && violation.Field is not null)
            .DistinctBy(violation => (violation.Field, violation.Code))
            .Select(violation => new FieldError(violation.Field!, violation.Code, violation.Args.ToArray()))
            .ToList();

        // Existing tiers (not the proposed one) that break the rules. This request cannot repair them.
        var existingTiers = all
            .Where(violation => violation.Tier is not null && !ReferenceEquals(violation.Tier, proposed))
            .Select(violation => violation.Tier!)
            .DistinctBy(tier => tier.Id)
            .ToList();
        var existingIds = existingTiers.Select(tier => tier.Id).ToList();
        var relatedRecords = existingTiers
            .Select(tier => new RelatedRecord(tier.Id, tier.MinPrice, tier.MinInclusive, tier.MaxPrice, tier.MaxInclusive))
            .ToList();

        if (fieldErrors.Count == 0 && existingIds.Count > 0 && all.All(violation => violation.Tier is not null))
        {
            // The stored set is already invalid and the proposed tier is not at fault: a state conflict, not a bad request.
            return new ApplicationError(ErrorCodes.Conflict, ErrorKind.Conflict) { RelatedIds = existingIds, RelatedRecords = relatedRecords };
        }

        // A set-level failure with nothing to point at keeps its own rule code, so the message can say what is wrong.
        var code = fieldErrors.Count == 0 && existingIds.Count == 0 && all.Count > 0 ? all[0].Code : ErrorCodes.ValidationFailed;
        return new ApplicationError(code, ErrorKind.Validation, fieldErrors.FirstOrDefault()?.Field)
        {
            FieldErrors = fieldErrors,
            RelatedIds = existingIds,
            RelatedRecords = relatedRecords,
        };
    }
}
