using HouseBroker.Application.Contracts;

namespace HouseBroker.Infrastructure;

/// <summary>Null current user for system operations.</summary>
public sealed class NullCurrentUserService : ICurrentUserService
{
    /// <inheritdoc />
    public Guid? UserId => null;

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
