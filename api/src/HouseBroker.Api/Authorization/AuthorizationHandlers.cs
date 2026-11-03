using System.Security.Claims;
using HouseBroker.Domain;
using HouseBroker.Domain.Shared;

namespace HouseBroker.Api.Authorization;

/// <summary>Maps permissions to scopes.</summary>
internal static class PermissionScopeMap
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        [Permissions.ListingsView] = Scopes.ListingsRead,
        [Permissions.ListingsContactView] = Scopes.ContactsRead,
        [Permissions.ListingsCreate] = Scopes.ListingsWrite,
        [Permissions.ListingsUpdate] = Scopes.ListingsWrite,
        [Permissions.ListingsDelete] = Scopes.ListingsWrite,
        [Permissions.ListingsImagesManage] = Scopes.ListingsWrite,
        [Permissions.CommissionView] = Scopes.CommissionRead,
        [Permissions.CommissionRatesManage] = Scopes.AdminCommission,
    };

    /// <summary>Gets the required scope for a permission.</summary>
    public static string? GetScope(string permission) => Map.TryGetValue(permission, out var scope) ? scope : null;

    /// <summary>Determines whether a permission is read-only.</summary>
    public static bool IsRead(string permission)
        => permission is Permissions.ListingsView or Permissions.ListingsContactView;
}

/// <summary>Requires a permission.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    /// <summary>Initializes the attribute.</summary>
    public HasPermissionAttribute(string permission)
    {
        Permission = permission;
        Policy = $"Permission:{permission}";
    }

    /// <summary>Gets the permission.</summary>
    public string Permission { get; }
}

/// <summary>Permission authorization requirement.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    /// <summary>Gets the permission.</summary>
    public string Permission { get; } = permission;
}

/// <summary>Resource owner requirement.</summary>
public sealed class OwnerRequirement : IAuthorizationRequirement
{
}

/// <summary>Handles permission requirements.</summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var requiredScope = PermissionScopeMap.GetScope(requirement.Permission);
        if (requiredScope is null)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        var scopes = GetScopes(context.User);
        if (!scopes.Contains(requiredScope, StringComparer.Ordinal))
        {
            context.Fail();
            return Task.CompletedTask;
        }

        var permissions = context.User.FindAll("permission").Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Partner tokens carry scopes but no permission claims for read-only access.
        if (!context.User.HasClaim(claim => claim.Type == "permission") &&
            PermissionScopeMap.IsRead(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }

    private static HashSet<string> GetScopes(ClaimsPrincipal principal)
    {
        var scopes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in principal.FindAll("scp"))
        {
            scopes.Add(claim.Value);
        }

        foreach (var claim in principal.FindAll("scope"))
        {
            foreach (var value in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                scopes.Add(value);
            }
        }

        return scopes;
    }
}

/// <summary>Handles resource ownership requirements.</summary>
public sealed class ResourceOwnerAuthorizationHandler : AuthorizationHandler<OwnerRequirement, Listing>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OwnerRequirement requirement, Listing resource)
    {
        var subject = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(subject, out var userId) && resource.BrokerId == userId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
