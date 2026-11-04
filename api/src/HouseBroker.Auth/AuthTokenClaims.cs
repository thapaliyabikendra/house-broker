using System.Collections.Immutable;
using System.Security.Claims;
using OpenIddict.Abstractions;

namespace HouseBroker.Auth;

/// <summary>
/// Builds the claims issued to an interactive user. Every claim sets its destinations explicitly:
/// OpenIddict drops claims without destinations, and the API reads roles, permissions and scopes
/// from the access token.
/// </summary>
public static class AuthTokenClaims
{
    /// <summary>Audience of the House Broker API; access tokens are only valid for this resource.</summary>
    public const string ApiAudience = "housebroker-api";

    /// <summary>Creates the identity for a signed-in user with the requested scopes.</summary>
    public static ClaimsIdentity CreateUserIdentity(
        string authenticationType,
        Guid userId,
        string? userName,
        string? email,
        IReadOnlyCollection<string> roles,
        IEnumerable<string> scopes)
    {
        var identity = new ClaimsIdentity(
            authenticationType,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, userId.ToString())
            .SetDestinations(OpenIddictConstants.Destinations.AccessToken));

        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Name, userName ?? email ?? userId.ToString())
            .SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken));

        if (!string.IsNullOrWhiteSpace(email))
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Email, email)
                .SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken));
        }

        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, role)
                .SetDestinations(OpenIddictConstants.Destinations.AccessToken));
        }

        foreach (var permission in AuthClaimMapper.GetPermissions(roles))
        {
            identity.AddClaim(new Claim(AuthClaimMapper.PermissionClaimType, permission)
                .SetDestinations(OpenIddictConstants.Destinations.AccessToken));
        }

        // OpenIddict owns the "scope" claim, so granted scopes travel as "scp" (the API reads both).
        foreach (var scope in scopes)
        {
            identity.AddClaim(new Claim(AuthClaimMapper.ScopeClaimType, scope)
                .SetDestinations(OpenIddictConstants.Destinations.AccessToken));
        }

        identity.SetAudiences(ImmutableArray.Create(ApiAudience));
        return identity;
    }
}
