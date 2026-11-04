namespace HouseBroker.Application.Contracts.Listings;

using System.Collections.ObjectModel;

/// <summary>Request to create a listing.</summary>
public sealed class CreateListingRequest
{
    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the property type identifier.</summary>
    public Guid PropertyTypeId { get; set; }

    /// <summary>Gets or sets the price.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the province.</summary>
    public string Province { get; set; } = string.Empty;

    /// <summary>Gets or sets the city.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Gets or sets the locality.</summary>
    public string Locality { get; set; } = string.Empty;

    /// <summary>Gets or sets the postal code.</summary>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of bedrooms.</summary>
    public int Bedrooms { get; set; }

    /// <summary>Gets or sets the number of bathrooms.</summary>
    public int Bathrooms { get; set; }

    /// <summary>Gets or sets the area in square feet.</summary>
    public decimal AreaSqFt { get; set; }

    /// <summary>Gets or sets feature identifiers.</summary>
    public Collection<Guid> FeatureIds { get; } = new();
}

/// <summary>Request to update a listing.</summary>
public sealed class UpdateListingRequest
{
    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the property type identifier.</summary>
    public Guid PropertyTypeId { get; set; }

    /// <summary>Gets or sets the price.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the province.</summary>
    public string Province { get; set; } = string.Empty;

    /// <summary>Gets or sets the city.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Gets or sets the locality.</summary>
    public string Locality { get; set; } = string.Empty;

    /// <summary>Gets or sets the postal code.</summary>
    public string PostalCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of bedrooms.</summary>
    public int Bedrooms { get; set; }

    /// <summary>Gets or sets the number of bathrooms.</summary>
    public int Bathrooms { get; set; }

    /// <summary>Gets or sets the area in square feet.</summary>
    public decimal AreaSqFt { get; set; }

    /// <summary>Gets or sets feature identifiers.</summary>
    public Collection<Guid> FeatureIds { get; } = new();

    /// <summary>Gets or sets the row version.</summary>
    public string? Version { get; set; }
}
