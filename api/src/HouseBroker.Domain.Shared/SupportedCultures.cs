namespace HouseBroker.Domain.Shared;

/// <summary>Supported cultures and defaults.</summary>
public static class SupportedCultures
{
    /// <summary>The default culture.</summary>
    public const string Default = "en";

    /// <summary>All supported cultures.</summary>
    public static readonly string[] All = new[] { "en", "ne" };

    /// <summary>The default currency code.</summary>
    public const string DefaultCurrency = "NPR";
}
