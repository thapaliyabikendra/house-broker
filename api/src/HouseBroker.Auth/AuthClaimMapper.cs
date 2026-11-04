using HouseBroker.Domain.Common;
using System.Security.Claims;

namespace HouseBroker.Auth;

/// <summary>Maps roles to permission claims and classifies claims for token destinations.</summary>
public static class AuthClaimMapper
{
    /// <summary>Claim type carrying one permission per value (read by the API).</summary>
    public const string PermissionClaimType = "permission";

    /// <summary>Claim type carrying one granted scope per value (read by the API).</summary>
    public const string ScopeClaimType = "scp";

    /// <summary>Roles a user may choose during self-registration.</summary>
    public static readonly string[] RegistrationRoles = [Roles.Seeker, Roles.Broker];

    /// <summary>Returns the permission set implied by the supplied roles.</summary>
    public static IReadOnlySet<string> GetPermissions(IEnumerable<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            if (role == Roles.Broker)
            {
                permissions.Add(AppPermissions.ListingsView);
                permissions.Add(AppPermissions.ListingsContactView);
                permissions.Add(AppPermissions.ListingsCreate);
                permissions.Add(AppPermissions.ListingsUpdate);
                permissions.Add(AppPermissions.ListingsDelete);
                permissions.Add(AppPermissions.ListingsImagesManage);
                permissions.Add(AppPermissions.CommissionView);
            }
            else if (role == Roles.Seeker)
            {
                permissions.Add(AppPermissions.ListingsView);
                permissions.Add(AppPermissions.ListingsContactView);
            }
            else if (role == Roles.Admin)
            {
                permissions.Add(AppPermissions.CommissionRatesManage);
            }
        }

        return permissions;
    }

    /// <summary>Indicates whether a claim should be delivered to both identity and access tokens.</summary>
    public static bool IsIdentityClaim(string claimType) =>
        claimType is ClaimTypes.Name or "name"
            or ClaimTypes.Email or "email"
            or ClaimTypes.MobilePhone or "phone_number";

    /// <summary>Returns the OpenID scope required to expose an identity claim, when applicable.</summary>
    public static string? GetIdentityScope(string claimType) =>
        claimType switch
        {
            ClaimTypes.Name or "name" => "profile",
            ClaimTypes.Email or "email" => "email",
            ClaimTypes.MobilePhone or "phone_number" => "phone",
            _ => null,
        };

    /// <summary>Indicates whether an identity claim may be emitted for the granted scopes.</summary>
    public static bool IsIdentityClaimAllowed(string claimType, IEnumerable<string> grantedScopes)
    {
        var scope = GetIdentityScope(claimType);
        if (scope is null)
        {
            return false;
        }

        return grantedScopes.Contains(scope, StringComparer.Ordinal);
    }
}
