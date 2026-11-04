using Microsoft.AspNetCore;
using HouseBroker.Auth.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Server.AspNetCore;

namespace HouseBroker.Auth.Controllers;

/// <summary>
/// End-session endpoint (<c>/connect/logout</c>). Ends the interactive Identity session, then lets
/// OpenIddict redirect to the post_logout_redirect_uri, which it only accepts when the client registered it.
/// </summary>
[ApiController]
public sealed class EndSessionController(SignInManager<ApplicationUser> signInManager) : ControllerBase
{
    /// <summary>
    /// Signs the user out and redirects to the client's registered post-logout URI.
    /// GET is required by OpenID Connect RP-initiated logout. Logout CSRF is accepted as a tradeoff:
    /// the worst outcome is that a user is signed out, and no data is changed or disclosed.
    /// </summary>
    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> LogoutAsync()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest();
        }

        await signInManager.SignOutAsync();

        // A null URI makes OpenIddict end the request without a redirect, which is the safe default.
        return SignOut(
            new AuthenticationProperties { RedirectUri = request.PostLogoutRedirectUri },
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
