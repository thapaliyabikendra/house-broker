namespace HouseBroker.Auth;

/// <summary>Database connection settings.</summary>
public sealed class DatabaseOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "ConnectionStrings";

    /// <summary>Gets or sets the House Broker connection string.</summary>
    public string HouseBroker { get; set; } = string.Empty;
}
