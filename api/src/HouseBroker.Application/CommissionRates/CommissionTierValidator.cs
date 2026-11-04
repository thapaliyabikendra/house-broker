using System.Runtime.CompilerServices;
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

    /// <summary>
    /// Gets the stored tier that actually broke a rule when the violation was re-attributed to another tier
    /// (for example a neighbour whose boundary moved with the edited tier), or null.
    /// </summary>
    public CommissionRate? RelatedTier { get; init; }

    /// <summary>
    /// Value equality over the code, field, arguments and tier identity. The generated record equality would compare
    /// <see cref="Args"/> by reference and tiers by the entity's own equality, neither of which is wanted here.
    /// </summary>
    public bool Equals(CommissionTierViolation? other)
        => other is not null
            && Code == other.Code
            && Field == other.Field
            && ReferenceEquals(Tier, other.Tier)
            && ReferenceEquals(RelatedTier, other.RelatedTier)
            && Args.SequenceEqual(other.Args);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Code);
        hash.Add(Field);
        hash.Add(Tier is null ? 0 : RuntimeHelpers.GetHashCode(Tier));
        hash.Add(RelatedTier is null ? 0 : RuntimeHelpers.GetHashCode(RelatedTier));
        foreach (var arg in Args)
        {
            hash.Add(arg);
        }

        return hash.ToHashCode();
    }
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

        // An open-ended tier below a bounded top tier is reported once here; the adjacency loop below skips it
        // so the same tier and field are not reported twice under two codes.
        CommissionRate? reportedTopOpenEnded = null;
        if (openEnded.Count > 0 && active[^1].MaxPrice is not null)
        {
            reportedTopOpenEnded = openEnded[0];
            violations.Add(new CommissionTierViolation(ErrorCodes.TierTopOpenEnded, reportedTopOpenEnded, "MaxPrice", reportedTopOpenEnded.MinPrice));
        }

        for (var index = 1; index < active.Count; index++)
        {
            var previous = active[index - 1];
            var current = active[index];

            if (previous.MaxPrice is null)
            {
                if (!ReferenceEquals(previous, reportedTopOpenEnded))
                {
                    violations.Add(new CommissionTierViolation(ErrorCodes.TierOpenEndedLast, previous, "MaxPrice", previous.MinPrice));
                }

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
    /// Validates the tier set at <paramref name="nowUtc"/> and at every later instant where an active tier becomes effective,
    /// so a change cannot be valid today and broken the moment a scheduled tier takes effect.
    /// Identical violations found at several instants are reported once.
    /// </summary>
    public static IReadOnlyList<CommissionTierViolation> ValidateTimeline(IEnumerable<CommissionRate> tiers, DateTime nowUtc)
    {
        var set = tiers.ToList();
        var instants = set
            .Where(tier => tier.IsActive && tier.EffectiveFrom > nowUtc)
            .Select(tier => tier.EffectiveFrom)
            .Append(nowUtc)
            .Distinct()
            .OrderBy(instant => instant);

        var violations = new List<CommissionTierViolation>();
        foreach (var instant in instants)
        {
            violations.AddRange(Validate(set, instant));
        }

        return violations.Distinct().ToList();
    }

    /// <summary>
    /// Builds the validation error for an admin write. Violations on the proposed tier become per-field entries;
    /// violations that only involve existing tiers cannot be fixed by fields of this request, so they produce a
    /// field-less error that names those tiers in <see cref="ApplicationError.RelatedRecords"/>.
    /// </summary>
    /// <param name="violations">The violations found by <see cref="Validate"/> or <see cref="ValidateTimeline"/>.</param>
    /// <param name="proposed">The tier being written.</param>
    /// <param name="storedSetInvalid">
    /// Whether the stored set was already invalid before this write. Only then is a field-less failure a state
    /// conflict; otherwise this request broke the set and the failure is a validation error. Defaults to true,
    /// which keeps the previous behaviour for callers that do not compute a baseline.
    /// </param>
    public static ApplicationError ToApplicationError(IEnumerable<CommissionTierViolation> violations, CommissionRate proposed, bool storedSetInvalid = true)
    {
        var all = violations.ToList();
        var fieldErrors = all
            .Where(violation => ReferenceEquals(violation.Tier, proposed) && violation.Field is not null)
            .DistinctBy(violation => (violation.Field, violation.Code))
            .Select(violation => new FieldError(violation.Field!, violation.Code, violation.Args.ToArray()))
            .ToList();

        // Existing tiers (not the proposed one) involved in a violation: either the tier that broke a rule, or the
        // stored neighbour whose cascaded boundary caused a violation reported against the proposed tier.
        var existingTiers = all
            .SelectMany(violation => new[] { violation.Tier, violation.RelatedTier })
            .Where(tier => tier is not null && !ReferenceEquals(tier, proposed))
            .Select(tier => tier!)
            .DistinctBy(tier => tier.Id)
            .ToList();
        var existingIds = existingTiers.Select(tier => tier.Id).ToList();
        var relatedRecords = existingTiers
            .Select(tier => new RelatedRecord(tier.Id, tier.MinPrice, tier.MinInclusive, tier.MaxPrice, tier.MaxInclusive))
            .ToList();

        if (storedSetInvalid && fieldErrors.Count == 0 && existingIds.Count > 0 && all.All(violation => violation.Tier is not null))
        {
            // The stored set was already invalid and the proposed tier is not at fault: a state conflict, not a bad request.
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
