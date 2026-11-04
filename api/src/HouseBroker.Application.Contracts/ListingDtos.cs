namespace HouseBroker.Application.Contracts;

using System.Collections.ObjectModel;

/// <summary>Image DTO.</summary>
public sealed class ListingImageDto
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the content type.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Gets or sets the content length.</summary>
    public long ContentLength { get; set; }

    /// <summary>Gets or sets whether this is the primary image.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Gets or sets the sort order.</summary>
    public int SortOrder { get; set; }
}

/// <summary>Feature DTO.</summary>
public sealed class ListingFeatureDto
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>Broker contact DTO.</summary>
public sealed class BrokerContactDto
{
    /// <summary>Gets or sets the name.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Gets or sets the email.</summary>
    public string? Email { get; set; }

    /// <summary>Gets or sets the phone number.</summary>
    public string? PhoneNumber { get; set; }
}

/// <summary>Listing summary DTO.</summary>
public sealed class ListingSummaryDto
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the price.</summary>
    public decimal Price { get; set; }

    /// <summary>Gets or sets the city.</summary>
    public string City { get; set; } = string.Empty;

    /// <summary>Gets or sets the locality.</summary>
    public string Locality { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of bedrooms.</summary>
    public int Bedrooms { get; set; }

    /// <summary>Gets or sets the number of bathrooms.</summary>
    public int Bathrooms { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the property type identifier.</summary>
    public Guid PropertyTypeId { get; set; }

    /// <summary>Gets or sets the primary image identifier used for the search thumbnail; null when the listing has no image.</summary>
    public Guid? PrimaryImageId { get; set; }

    /// <summary>Gets or sets the created timestamp.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>Listing detail DTO.</summary>
public class ListingDetailDto
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

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

    /// <summary>Gets or sets the status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Gets or sets the images.</summary>
    public Collection<ListingImageDto> Images { get; } = new();

    /// <summary>Gets or sets the features.</summary>
    public Collection<ListingFeatureDto> Features { get; } = new();

    /// <summary>Gets or sets the broker contact.</summary>
    public BrokerContactDto? BrokerContact { get; set; }

    /// <summary>Gets or sets the created timestamp.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the row version.</summary>
    public string? Version { get; set; }
}

/// <summary>Owner listing DTO.</summary>
public sealed class OwnerListingDto : ListingDetailDto
{
    /// <summary>Gets or sets the commission amount.</summary>
    public decimal CommissionAmount { get; set; }

    /// <summary>Gets or sets the commission rate percent.</summary>
    public decimal CommissionRatePercent { get; set; }

    /// <summary>Gets or sets the commission rate identifier.</summary>
    public Guid CommissionRateId { get; set; }
}

/// <summary>Paged result.</summary>
public sealed class PagedResult<T>
{
    /// <summary>Gets or sets the items.</summary>
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

    /// <summary>Gets or sets the total count.</summary>
    public int Total { get; set; }

    /// <summary>Gets or sets the page.</summary>
    public int Page { get; set; }

    /// <summary>Gets or sets the page size.</summary>
    public int PageSize { get; set; }
}

/// <summary>Search query.</summary>
public sealed class ListingSearchQuery
{
    /// <summary>Gets or sets the location text.</summary>
    public string? Location { get; set; }

    /// <summary>Gets or sets the minimum price.</summary>
    public decimal? MinPrice { get; set; }

    /// <summary>Gets or sets the maximum price.</summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>Gets or sets the property type identifier.</summary>
    public Guid? PropertyTypeId { get; set; }

    /// <summary>Gets or sets the minimum number of bedrooms.</summary>
    public int? Bedrooms { get; set; }

    /// <summary>Gets or sets the feature identifiers.</summary>
    public Collection<Guid> FeatureIds { get; } = new();

    /// <summary>Gets or sets the page.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Gets or sets the page size.</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>Gets or sets the sort order.</summary>
    public string? Sort { get; set; }
}
