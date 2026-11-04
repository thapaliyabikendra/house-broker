using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;

namespace HouseBroker.Auth;

/// <summary>Registers or updates an OAuth client using the supplied configuration.</summary>
public sealed class ClientRegistrationService(IOpenIddictApplicationManager applicationManager, IConfiguration configuration)
{
    /// <summary>Registers or updates the configured client.</summary>
    public async Task RegisterAsync(CancellationToken cancellationToken = default)
    {
        var clientId = configuration["Client:Id"] ?? throw new InvalidOperationException("Client:Id is not configured.");
        var clientSecret = configuration["Client:Secret"];
        var clientType = string.IsNullOrWhiteSpace(clientSecret)
            ? OpenIddictConstants.ClientTypes.Public
            : OpenIddictConstants.ClientTypes.Confidential;
        var redirectUris = configuration.GetSection("Client:RedirectUris").Get<string[]>() ?? Array.Empty<string>();
        var scopes = configuration.GetSection("Client:Scopes").Get<string[]>() ?? new[] { "openid", "profile" };

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = clientType,
            DisplayName = configuration["Client:Name"] ?? clientId,
        };

        if (!string.IsNullOrWhiteSpace(clientSecret))
        {
            descriptor.ClientSecret = clientSecret;
        }

        foreach (var uri in redirectUris)
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            {
                descriptor.RedirectUris.Add(parsed);
            }
        }

        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        if (redirectUris.Length > 0)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        }
        else
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
        }

        foreach (var scope in scopes)
        {
            descriptor.Permissions.Add("scp:" + scope);
        }

        var existing = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (existing is null)
        {
            await applicationManager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
        }
    }
}
