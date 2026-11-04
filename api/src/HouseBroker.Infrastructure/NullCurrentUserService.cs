using HouseBroker.Application.Contracts;

namespace HouseBroker.Infrastructure;

/// <summary>Null current user for system operations.</summary>
public sealed class NullCurrentUserService : ICurrentUserService
{
    /// <inheritdoc />
    public Guid? UserId => null;

    /// <inheritdoc />
    public string? FullName => null;

    /// <inheritdoc />
    public string? Email => null;

    /// <inheritdoc />
    public string? PhoneNumber => null;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Roles => Array.Empty<string>();

    /// <inheritdoc />
    public IReadOnlyCollection<string> Permissions => Array.Empty<string>();

    /// <inheritdoc />
    public IReadOnlyCollection<string> Scopes => Array.Empty<string>();

    /// <inheritdoc />
    public string? ClientId => null;

    /// <inheritdoc />
    public bool IsAuthenticated => false;
}
