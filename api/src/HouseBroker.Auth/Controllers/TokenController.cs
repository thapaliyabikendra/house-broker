using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;

namespace HouseBroker.Auth.Controllers;

[ApiController]
public sealed class TokenController(IOpenIddictApplicationManager applicationManager) : ControllerBase
{
    [HttpPost("~/connect/token")]
    [Produces("application/json")]
    public async Task<IActionResult> ExchangeAsync()
    {
        var request = HttpContext.GetOpenIddictServerRequest();
        if (request is null)
        {
            return BadRequest();
        }

        if (request.IsClientCredentialsGrantType())
        {
            var application = await applicationManager.FindByClientIdAsync(request.ClientId ?? string.Empty);
            if (application is null
                || !await applicationManager.ValidateClientSecretAsync(application, request.ClientSecret ?? string.Empty))
            {
                return Forbid();
            }

            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var subject = await applicationManager.GetIdAsync(application) ?? request.ClientId!;
            identity.AddClaim(OpenIddictConstants.Claims.Subject, subject, OpenIddictConstants.Destinations.AccessToken);
            identity.AddClaim(OpenIddictConstants.Claims.ClientId, request.ClientId!, OpenIddictConstants.Destinations.AccessToken);

            foreach (var scope in request.GetScopes())
            {
                identity.AddClaim("scope", scope, OpenIddictConstants.Destinations.AccessToken);
            }

            var principal = new ClaimsPrincipal(identity);
            principal.SetScopes(request.GetScopes());

            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var authenticateResult = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
        {
            return Forbid();
        }

        var exchangedPrincipal = authenticateResult.Principal;
        exchangedPrincipal.SetScopes(request.GetScopes());

        return SignIn(exchangedPrincipal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
