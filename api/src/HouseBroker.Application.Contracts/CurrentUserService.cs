namespace HouseBroker.Application.Contracts;

/// <summary>Current user context.</summary>
public interface ICurrentUserService
{
    /// <summary>Gets the subject identifier.</summary>
    Guid? UserId { get; }

    /// <summary>Gets the user's full name.</summary>
    string? FullName { get; }

    /// <summary>Gets the user's email address.</summary>
    string? Email { get; }

    /// <summary>Gets the user's phone number.</summary>
    string? PhoneNumber { get; }

    /// <summary>Gets the user roles.</summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>Gets the user permissions.</summary>
    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>Gets the client scopes.</summary>
    IReadOnlyCollection<string> Scopes { get; }

    /// <summary>Gets the client identifier.</summary>
    string? ClientId { get; }

    /// <summary>Determines whether the user is authenticated.</summary>
    bool IsAuthenticated { get; }
}
