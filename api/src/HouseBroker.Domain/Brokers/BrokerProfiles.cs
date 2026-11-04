namespace HouseBroker.Domain;

/// <summary>Read-model of broker contact details.</summary>
public sealed class BrokerProfile
{
    private BrokerProfile()
    {
    }

    /// <summary>Initializes a profile.</summary>
    public static BrokerProfile Create(Guid brokerId, string fullName, string? email, string? phoneNumber)
        => new()
        {
            BrokerId = brokerId,
            FullName = fullName,
            Email = email,
            PhoneNumber = phoneNumber,
        };

    /// <summary>Gets the broker identifier.</summary>
    public Guid BrokerId { get; internal set; }

    /// <summary>Gets the full name.</summary>
    public string FullName { get; internal set; } = string.Empty;

    /// <summary>Gets the email.</summary>
    public string? Email { get; internal set; }

    /// <summary>Gets the phone number.</summary>
    public string? PhoneNumber { get; internal set; }

    /// <summary>Gets the created timestamp.</summary>
    public DateTime CreatedAt { get; internal set; }

    /// <summary>Gets the creator.</summary>
    public Guid? CreatedBy { get; internal set; }

    /// <summary>Gets the updated timestamp.</summary>
    public DateTime UpdatedAt { get; internal set; }

    /// <summary>Gets the updater.</summary>
    public Guid? UpdatedBy { get; internal set; }
}
