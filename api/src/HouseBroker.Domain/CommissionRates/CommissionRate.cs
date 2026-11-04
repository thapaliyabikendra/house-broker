namespace HouseBroker.Domain;

/// <summary>Commission rate tier.</summary>
public sealed class CommissionRate
{
    private CommissionRate()
    {
    }

    /// <summary>Initializes a rate.</summary>
    public static CommissionRate Create(decimal minPrice, bool minInclusive, decimal? maxPrice, bool maxInclusive, decimal ratePercent, bool isActive, DateTime effectiveFrom)
        => new()
        {
            Id = Guid.CreateVersion7(),
            MinPrice = minPrice,
            MinInclusive = minInclusive,
            MaxPrice = maxPrice,
            MaxInclusive = maxInclusive,
            RatePercent = ratePercent,
            IsActive = isActive,
            EffectiveFrom = effectiveFrom,
        };

    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the minimum price.</summary>
    public decimal MinPrice { get; private set; }

    /// <summary>Gets whether the minimum is inclusive.</summary>
    public bool MinInclusive { get; private set; }

    /// <summary>Gets the maximum price.</summary>
    public decimal? MaxPrice { get; private set; }

    /// <summary>Gets whether the maximum is inclusive.</summary>
    public bool MaxInclusive { get; private set; }

    /// <summary>Gets the rate percent.</summary>
    public decimal RatePercent { get; private set; }

    /// <summary>Gets whether the rate is active.</summary>
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

    /// <summary>Updates tier values.</summary>
    public void Update(decimal minPrice, bool minInclusive, decimal? maxPrice, bool maxInclusive, decimal ratePercent, bool isActive, DateTime effectiveFrom)
    {
        MinPrice = minPrice;
        MinInclusive = minInclusive;
        MaxPrice = maxPrice;
        MaxInclusive = maxInclusive;
        RatePercent = ratePercent;
        IsActive = isActive;
        EffectiveFrom = effectiveFrom;
    }

    /// <summary>Determines whether this tier covers a price.</summary>
    public bool Covers(decimal price)
    {
        if (MinInclusive ? price < MinPrice : price <= MinPrice)
        {
            return false;
        }

        if (MaxPrice is decimal max)
        {
            if (MaxInclusive ? price > max : price >= max)
            {
                return false;
            }
        }

        return true;
    }
}
