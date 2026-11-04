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
        var identity = AuthTokenClaims.CreateUserIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            user.Id,
            user.UserName,
            user.Email,
            roles.ToList(),
            request.GetScopes());

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
