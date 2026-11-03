namespace HouseBroker.Application.Contracts;

/// <summary>Commission rate DTO.</summary>
public sealed class CommissionRateDto
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the minimum price.</summary>
    public decimal MinPrice { get; set; }

    /// <summary>Gets or sets whether the minimum is inclusive.</summary>
    public bool MinInclusive { get; set; }

    /// <summary>Gets or sets the maximum price.</summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>Gets or sets whether the maximum is inclusive.</summary>
    public bool MaxInclusive { get; set; }

    /// <summary>Gets or sets the rate percent.</summary>
    public decimal RatePercent { get; set; }

    /// <summary>Gets or sets whether the rate is active.</summary>
    public bool IsActive { get; set; }

    /// <summary>Gets or sets the effective-from timestamp.</summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>Gets or sets the version.</summary>
    public string? Version { get; set; }
}

/// <summary>Commission rate write request.</summary>
public sealed class CommissionRateRequest
{
    /// <summary>Gets or sets the minimum price.</summary>
    public decimal MinPrice { get; set; }

    /// <summary>Gets or sets whether the minimum is inclusive.</summary>
    public bool MinInclusive { get; set; } = true;

    /// <summary>Gets or sets the maximum price.</summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>Gets or sets whether the maximum is inclusive.</summary>
    public bool MaxInclusive { get; set; }

    /// <summary>Gets or sets the rate percent.</summary>
    public decimal RatePercent { get; set; }

    /// <summary>Gets or sets whether the rate is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Gets or sets the effective-from timestamp.</summary>
    public DateTime EffectiveFrom { get; set; }
}
