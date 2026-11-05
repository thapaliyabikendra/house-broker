using HouseBroker.Domain.Common;

namespace HouseBroker.Domain;

/// <summary>
/// Commission rate band. Each row stores only its upper bound; the lower bound is derived from the
/// previous band in <see cref="CommissionTierSet"/> order, so bands are contiguous by construction.
/// </summary>
public sealed class CommissionRate
{
    private CommissionRate()
    {
    }

    /// <summary>Initializes a rate band.</summary>
    /// <param name="maxPrice">The upper bound, or null for the open-ended top band.</param>
    /// <param name="maxInclusive">Whether the upper bound is inclusive; forced to false when <paramref name="maxPrice"/> is null.</param>
    /// <param name="ratePercent">The flat rate applied to the whole price, in (0, 100].</param>
    /// <param name="effectiveFrom">The instant the band becomes effective, in UTC.</param>
    public static CommissionRate Create(decimal? maxPrice, bool maxInclusive, decimal ratePercent, DateTime effectiveFrom)
    {
        Validate(maxPrice, ratePercent);

        return new()
        {
            Id = Guid.CreateVersion7(),
            MaxPrice = maxPrice,
            MaxInclusive = maxPrice is null ? false : maxInclusive,
            RatePercent = ratePercent,
            IsActive = true,
            EffectiveFrom = effectiveFrom,
        };
    }

    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the upper bound, or null for the open-ended top band.</summary>
    public decimal? MaxPrice { get; private set; }

    /// <summary>Gets whether the upper bound is inclusive. Always false for an open-ended band.</summary>
    public bool MaxInclusive { get; private set; }

    /// <summary>Gets the rate percent.</summary>
    public decimal RatePercent { get; private set; }

    /// <summary>Gets whether the band is active. A retired band is kept for history but ignored by pricing.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Gets the effective-from timestamp.</summary>
    public DateTime EffectiveFrom { get; private set; }

    /// <summary>Gets the created timestamp.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Gets the creator.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>Gets the updated timestamp.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Gets the updater.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Gets the concurrency token.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "EF Core requires a byte array for rowversion concurrency tokens.")]
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>Updates the band's editable values.</summary>
    /// <param name="maxPrice">The upper bound, or null for the open-ended top band.</param>
    /// <param name="maxInclusive">Whether the upper bound is inclusive; forced to false when <paramref name="maxPrice"/> is null.</param>
    /// <param name="ratePercent">The flat rate applied to the whole price, in (0, 100].</param>
    /// <param name="effectiveFrom">The instant the band becomes effective, in UTC.</param>
    public void Update(decimal? maxPrice, bool maxInclusive, decimal ratePercent, DateTime effectiveFrom)
    {
        Validate(maxPrice, ratePercent);

        MaxPrice = maxPrice;
        MaxInclusive = maxPrice is null ? false : maxInclusive;
        RatePercent = ratePercent;
        EffectiveFrom = effectiveFrom;
    }

    /// <summary>Retires the band without deleting it, so listing snapshots keep a resolvable rate id.</summary>
    public void Retire() => IsActive = false;

    /// <summary>Reactivates a retired band after the active set has been re-validated.</summary>
    public void Reactivate() => IsActive = true;

    /// <summary>Promotes the band to the open-ended top band.</summary>
    public void PromoteToOpenEnded()
    {
        MaxPrice = null;
        MaxInclusive = false;
    }

    /// <summary>
    /// Creates a detached copy that preserves identity. The admin service validates a proposed state on copies
    /// before mutating the tracked entity, and the copies must keep the stored id for conflict reporting.
    /// </summary>
    public CommissionRate DetachedCopy()
    {
        var copy = (CommissionRate)MemberwiseClone();
        copy.RowVersion = (byte[])RowVersion.Clone();
        return copy;
    }

    private static void Validate(decimal? maxPrice, decimal ratePercent)
    {
        if (maxPrice is decimal max && max <= 0m)
        {
            throw new DomainException(ErrorCodes.OutOfRange, nameof(MaxPrice));
        }

        if (ratePercent <= 0m || ratePercent > 100m)
        {
            throw new DomainException(ErrorCodes.OutOfRange, nameof(RatePercent));
        }
    }
}
