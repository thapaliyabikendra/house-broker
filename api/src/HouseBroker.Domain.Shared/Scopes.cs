namespace HouseBroker.Domain.Shared;

/// <summary>OAuth scopes granted to clients.</summary>
public static class Scopes
{
    /// <summary>Read access to listings.</summary>
    public const string ListingsRead = "listings.read";

    /// <summary>Read access to broker contact details.</summary>
    public const string ContactsRead = "contacts.read";

    /// <summary>Write access to listings.</summary>
    public const string ListingsWrite = "listings.write";

    /// <summary>Read access to commission on own listings.</summary>
    public const string CommissionRead = "commission.read";

    /// <summary>Administrative access to commission rate tiers.</summary>
    public const string AdminCommission = "admin.commission";

    /// <summary>OpenID Connect scope.</summary>
    public const string OpenId = "openid";

    /// <summary>Profile scope.</summary>
    public const string Profile = "profile";

    /// <summary>Email scope.</summary>
    public const string Email = "email";

    /// <summary>Phone number scope.</summary>
    public const string Phone = "phone";

    /// <summary>Offline access scope.</summary>
    public const string OfflineAccess = "offline_access";
}
