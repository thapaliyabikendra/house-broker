namespace HouseBroker.Application.Contracts;

/// <summary>Property type DTO.</summary>
public sealed class PropertyTypeDto
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the sort order.</summary>
    public int SortOrder { get; set; }
}

/// <summary>Feature DTO.</summary>
public sealed class FeatureDto
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the sort order.</summary>
    public int SortOrder { get; set; }
}

/// <summary>Deployment metadata DTO.</summary>
public sealed class MetaDto
{
    /// <summary>Gets or sets the default culture.</summary>
    public string DefaultCulture { get; set; } = "en";

    /// <summary>Gets or sets the supported cultures.</summary>
    public IReadOnlyList<string> SupportedCultures { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the currency code.</summary>
    public string CurrencyCode { get; set; } = "NPR";

    /// <summary>Gets or sets the maximum page size.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>Gets or sets the maximum search text length.</summary>
    public int MaxSearchTextLength { get; set; } = 100;
}
