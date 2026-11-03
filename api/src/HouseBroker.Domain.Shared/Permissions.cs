namespace HouseBroker.Domain.Shared;

/// <summary>Permission claims carried by user access tokens.</summary>
public static class Permissions
{
    /// <summary>May view listings.</summary>
    public const string ListingsView = "listings:view";

    /// <summary>May view broker contact details.</summary>
    public const string ListingsContactView = "listings:contact:view";

    /// <summary>May create listings.</summary>
    public const string ListingsCreate = "listings:create";

    /// <summary>May update own listings.</summary>
    public const string ListingsUpdate = "listings:update";

    /// <summary>May delete own listings.</summary>
    public const string ListingsDelete = "listings:delete";

    /// <summary>May manage images on own listings.</summary>
    public const string ListingsImagesManage = "listings:images:manage";

    /// <summary>May view commission on own listings.</summary>
    public const string CommissionView = "commission:view";

    /// <summary>May manage commission rate tiers.</summary>
    public const string CommissionRatesManage = "commission:rates:manage";
}
