using HouseBroker.Application.Contracts;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;

namespace HouseBroker.Application.CommissionRates;

/// <summary>One broken rule in a commission tier set, with the tier and request field it concerns.</summary>
/// <param name="Code">The stable error code (for example <c>validation.out_of_range</c>).</param>
/// <param name="Tier">The tier the rule was checked on. Null when the rule is about the set as a whole.</param>
/// <param name="Field">The tier property at fault (for example <c>MaxPrice</c>), or null when no single property is at fault.</param>
public sealed record CommissionTierViolation(string Code, CommissionRate? Tier, string? Field);

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
            violations.Add(new CommissionTierViolation(ErrorCodes.Required, null, null));
            return violations;
        }

        var openEnded = active.Where(tier => tier.MaxPrice is null).ToList();
        if (openEnded.Count != 1)
        {
            // Point at the open-ended candidate when there is one; otherwise at the top tier.
            var subject = openEnded.Count > 1 ? openEnded[0] : active[^1];
            violations.Add(new CommissionTierViolation(ErrorCodes.ValidationFailed, subject, "MaxPrice"));
        }

        var first = active[0];
        if (first.MinPrice != 0m)
        {
            violations.Add(new CommissionTierViolation(ErrorCodes.OutOfRange, first, "MinPrice"));
        }
        else if (!first.MinInclusive)
        {
            violations.Add(new CommissionTierViolation(ErrorCodes.ValidationFailed, first, "MinInclusive"));
        }

        foreach (var tier in active)
        {
            if (tier.MaxPrice is decimal max && max <= tier.MinPrice)
            {
                violations.Add(new CommissionTierViolation(ErrorCodes.OutOfRange, tier, "MaxPrice"));
            }
        }

        if (openEnded.Count > 0 && active[^1].MaxPrice is not null)
        {
            violations.Add(new CommissionTierViolation(ErrorCodes.ValidationFailed, openEnded[0], "MaxPrice"));
        }

        for (var index = 1; index < active.Count; index++)
        {
            var previous = active[index - 1];
            var current = active[index];

            if (previous.MaxPrice is null)
            {
                violations.Add(new CommissionTierViolation(ErrorCodes.ValidationFailed, previous, "MaxPrice"));
                continue;
            }

            if (previous.MaxPrice.Value != current.MinPrice)
            {
                violations.Add(new CommissionTierViolation(ErrorCodes.ValidationFailed, previous, "MaxPrice"));
                violations.Add(new CommissionTierViolation(ErrorCodes.ValidationFailed, current, "MinPrice"));
                continue;
            }

            if (previous.MaxInclusive == current.MinInclusive)
            {
                violations.Add(new CommissionTierViolation(ErrorCodes.ValidationFailed, previous, "MaxInclusive"));
                violations.Add(new CommissionTierViolation(ErrorCodes.ValidationFailed, current, "MinInclusive"));
            }
        }

        return violations;
    }

    /// <summary>
    /// Builds the validation error for an admin write. Violations on the proposed tier become per-field entries;
    /// violations that only involve existing tiers cannot be fixed by fields of this request, so they produce a
    /// field-less validation error.
    /// </summary>
    /// <param name="violations">The violations found by <see cref="Validate"/>.</param>
    /// <param name="proposed">The tier being written.</param>
    public static ApplicationError ToApplicationError(IEnumerable<CommissionTierViolation> violations, CommissionRate proposed)
    {
        var all = violations.ToList();
        var fieldErrors = all
            .Where(violation => ReferenceEquals(violation.Tier, proposed) && violation.Field is not null)
            .Select(violation => new FieldError(violation.Field!, violation.Code))
            .Distinct()
            .ToList();

        // Existing tiers (not the proposed one) that break the rules. This request cannot repair them.
        var existingIds = all
            .Where(violation => violation.Tier is not null && !ReferenceEquals(violation.Tier, proposed))
            .Select(violation => violation.Tier!.Id)
            .Distinct()
            .ToList();

        if (fieldErrors.Count == 0 && existingIds.Count > 0 && all.All(violation => violation.Tier is not null))
        {
            // The stored set is already invalid and the proposed tier is not at fault: a state conflict, not a bad request.
            return new ApplicationError(ErrorCodes.Conflict, ErrorKind.Conflict) { RelatedIds = existingIds };
        }

        return new ApplicationError(ErrorCodes.ValidationFailed, ErrorKind.Validation, fieldErrors.FirstOrDefault()?.Field)
        {
            FieldErrors = fieldErrors,
            RelatedIds = existingIds,
        };
    }
}
