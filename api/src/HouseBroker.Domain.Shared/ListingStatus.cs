namespace HouseBroker.Domain.Shared;

/// <summary>Lifecycle status of a listing.</summary>
public enum ListingStatus
{
    /// <summary>Draft listing, not visible to seekers.</summary>
    Draft = 0,

    /// <summary>Published listing, visible to seekers.</summary>
    Active = 1,

    /// <summary>Sold listing.</summary>
    Sold = 2,

    /// <summary>Archived listing.</summary>
    Archived = 3,
}