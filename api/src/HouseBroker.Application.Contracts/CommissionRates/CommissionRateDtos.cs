using System.Text.Json.Serialization;

namespace HouseBroker.Application.Contracts.CommissionRates;

/// <summary>Commission rate band DTO.</summary>
public sealed class CommissionRateDto
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the derived lower bound for display, or null when the band is not yet effective.
    /// Retired bands keep their last derived lower bound. The lower bound is not stored; it comes from the
    /// previous band in the full set.
    /// </summary>
    public decimal? MinPrice { get; set; }

    /// <summary>
    /// Gets or sets whether the derived lower bound is inclusive for display, or null when the band is retired or
    /// not yet effective.
    /// </summary>
    public bool? MinInclusive { get; set; }

    /// <summary>Gets or sets the upper bound, or null for the open-ended top band.</summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>Gets or sets whether the upper bound is inclusive.</summary>
    public bool MaxInclusive { get; set; }

    /// <summary>Gets or sets the rate percent.</summary>
    public decimal RatePercent { get; set; }

    /// <summary>Gets or sets whether the band is active.</summary>
    public bool IsActive { get; set; }

    /// <summary>Gets or sets the effective-from timestamp.</summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>Gets or sets the version.</summary>
    public string? Version { get; set; }
}

/// <summary>Commission rate band write request.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CommissionRateRequest
{
    /// <summary>Gets or sets the upper bound, or null for the open-ended top band.</summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>Gets or sets whether the upper bound is inclusive; ignored when <see cref="MaxPrice"/> is null.</summary>
    public bool MaxInclusive { get; set; }

    /// <summary>Gets or sets the rate percent, in (0, 100].</summary>
    public decimal RatePercent { get; set; }

    /// <summary>Gets or sets the effective-from timestamp. Defaults to now when omitted.</summary>
    public DateTime EffectiveFrom { get; set; }
}
