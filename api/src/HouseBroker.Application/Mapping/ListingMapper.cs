using HouseBroker.Domain;
using HouseBroker.Application.Contracts;
using Riok.Mapperly.Abstractions;

namespace HouseBroker.Application.Mapping;

/// <summary>Generated mapping profiles for listing entities and DTOs.</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public sealed partial class ListingMapper
{
    /// <summary>Shared instance for static mapping helpers.</summary>
    public static readonly ListingMapper Instance = new();

    /// <summary>Maps a listing to a public detail DTO without commission fields.</summary>
    [MapProperty(nameof(Listing.RowVersion), nameof(ListingDetailDto.Version))]
    public partial ListingDetailDto ToDetail(Listing source);

    /// <summary>Maps a listing to an owner detail DTO including commission.</summary>
    [MapProperty(nameof(Listing.RowVersion), nameof(ListingDetailDto.Version))]
    public partial OwnerListingDto ToOwner(Listing source);

    /// <summary>Maps a listing to a summary DTO.</summary>
    public partial ListingSummaryDto ToSummary(Listing source);

    /// <summary>Maps a listing image to its DTO.</summary>
    public partial ListingImageDto ToImage(ListingImage source);

    /// <summary>Maps a broker profile to a contact DTO.</summary>
    public static BrokerContactDto ToBrokerContact(BrokerProfile source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            FullName = source.FullName,
            Email = source.Email,
            PhoneNumber = source.PhoneNumber,
        };
    }

    /// <summary>Maps a listing feature to its DTO.</summary>
    public static ListingFeatureDto ToFeature(ListingFeature source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new()
        {
            Id = source.FeatureId,
            Code = source.Feature?.Code ?? string.Empty,
            Name = source.Feature?.Name ?? string.Empty,
        };
    }

    /// <summary>Maps a commission rate to its DTO.</summary>
    [MapProperty(nameof(CommissionRate.RowVersion), nameof(CommissionRateDto.Version))]
    public partial CommissionRateDto ToCommissionRate(CommissionRate source);

    /// <summary>Maps a property type to its DTO.</summary>
    public partial PropertyTypeDto ToPropertyType(PropertyType source);

    /// <summary>Maps a feature lookup to its DTO.</summary>
    public partial FeatureDto ToFeatureLookup(Feature source);

    /// <summary>Converts a listing status to its string representation.</summary>
    private static string MapStatus(HouseBroker.Domain.Common.ListingStatus status) => status.ToString();

    /// <summary>Converts a row version to its base64 representation.</summary>
    private static string MapVersion(byte[] rowVersion) => Convert.ToBase64String(rowVersion);
}
