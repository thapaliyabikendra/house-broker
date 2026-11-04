using System.Security.Claims;
using HouseBroker.Application.Contracts;

namespace HouseBroker.Api.Authorization;

/// <summary>Current user implementation.</summary>
public sealed class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    /// <inheritdoc />
    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirst("sub")?.Value ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    /// <inheritdoc />
    public string? FullName => User?.FindFirst("name")?.Value ?? User?.FindFirst(ClaimTypes.Name)?.Value;

    /// <inheritdoc />
    public string? Email => User?.FindFirst("email")?.Value ?? User?.FindFirst(ClaimTypes.Email)?.Value;

    /// <inheritdoc />
    public string? PhoneNumber => User?.FindFirst("phone_number")?.Value ?? User?.FindFirst(ClaimTypes.MobilePhone)?.Value;

    /// <inheritdoc />
    public IReadOnlyCollection<string> Roles => User?.FindAll("role").Select(claim => claim.Value).ToArray() ?? Array.Empty<string>();

    /// <inheritdoc />
    public IReadOnlyCollection<string> Permissions => User?.FindAll("permission").Select(claim => claim.Value).ToArray() ?? Array.Empty<string>();

    /// <inheritdoc />
    public IReadOnlyCollection<string> Scopes => User?.FindAll("scp").Select(claim => claim.Value)
        .Concat(User?.FindAll("scope").Select(claim => claim.Value).SelectMany(value => value.Split(' ')) ?? Array.Empty<string>())
        .Distinct(StringComparer.Ordinal)
        .ToArray() ?? Array.Empty<string>();

    /// <inheritdoc />
    public string? ClientId => User?.FindFirst("client_id")?.Value;

    /// <inheritdoc />
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
}
