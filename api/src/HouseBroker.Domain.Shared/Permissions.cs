namespace HouseBroker.Domain.Common;

/// <summary>Permission claims carried by user access tokens.</summary>
public static class AppPermissions
{
    /// <summary>May view the My Listings page (owned listings, including commission).</summary>
    public const string MyListings = "MyListings";

    /// <summary>May create listings.</summary>
    public const string MyListingsCreate = "MyListings.Create";

    /// <summary>May update own listings.</summary>
    public const string MyListingsUpdate = "MyListings.Update";

    /// <summary>May delete own listings.</summary>
    public const string MyListingsDelete = "MyListings.Delete";

    /// <summary>May manage images on own listings.</summary>
    public const string MyListingsImagesManage = "MyListings.ImagesManage";

    /// <summary>May view commission rate tiers (admin page).</summary>
    public const string CommissionRates = "CommissionRates";

    /// <summary>May create commission rate tiers.</summary>
    public const string CommissionRatesCreate = "CommissionRates.Create";

    /// <summary>May update commission rate tiers.</summary>
    public const string CommissionRatesUpdate = "CommissionRates.Update";

    /// <summary>May retire (soft delete) commission rate tiers.</summary>
    public const string CommissionRatesDelete = "CommissionRates.Delete";
}
