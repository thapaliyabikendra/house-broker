using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using HouseBroker.Auth.Data;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace HouseBroker.Auth.Controllers;

/// <summary>
/// Authorization code endpoint (<c>/connect/authorize</c>). Requires an interactive Identity session;
/// anonymous requests are sent to the sign-in page and resumed here afterwards.
/// </summary>
[ApiController]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public sealed class AuthorizationController(
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration) : ControllerBase
{
    private static readonly System.Collections.Immutable.ImmutableArray<string> ApiAudience =
        System.Collections.Immutable.ImmutableArray.Create("housebroker-api");

    /// <summary>Issues an authorization code for an authenticated user, or redirects to sign-in.</summary>
    [HttpGet("~/connect/authorize")]
    public async Task<IActionResult> AuthorizeAsync()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest();
        }

        var session = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var user = session.Succeeded && session.Principal is not null
            ? await userManager.GetUserAsync(session.Principal)
            : null;

        if (user is null)
        {
            // Resume this exact request (client, PKCE challenge, scopes) after the user signs in.
            var pathBase = configuration["PathBase"] ?? "/sso";
            var resumeUrl = Request.PathBase + Request.Path + Request.QueryString;
            return Redirect($"{pathBase}/Login?returnUrl={Uri.EscapeDataString(resumeUrl)}");
        }

        var roles = await userManager.GetRolesAsync(user);

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);

        // Destinations are set explicitly on each claim so tokens carry them regardless of handler order.
        identity.AddClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString(), OpenIddictConstants.Destinations.AccessToken);
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Name, user.UserName ?? user.Email ?? user.Id.ToString())
            .SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken));
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Email, user.Email)
                .SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken));
        }

        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Role, role).SetDestinations(OpenIddictConstants.Destinations.AccessToken));
        }

        // Permissions are the authorization source for the API (effective access = permission ∩ scope).
        foreach (var permission in AuthClaimMapper.GetPermissions(roles))
        {
            identity.AddClaim(new Claim("permission", permission).SetDestinations(OpenIddictConstants.Destinations.AccessToken));
        }

        // Granted scopes travel as scp claims (OpenIddict owns the "scope" claim) so the API can intersect them with permissions.
        foreach (var scope in request.GetScopes())
        {
            identity.AddClaim(new Claim("scp", scope).SetDestinations(OpenIddictConstants.Destinations.AccessToken));
        }

        identity.SetAudiences(ApiAudience);
        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
