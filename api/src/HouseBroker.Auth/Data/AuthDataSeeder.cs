using Microsoft.AspNetCore.Identity;
using HouseBroker.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore;

namespace HouseBroker.Auth.Data;

/// <summary>Idempotent seeder for Identity roles, users and clients.</summary>
public sealed class AuthDataSeeder(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOpenIddictApplicationManager applicationManager,
    ILogger<AuthDataSeeder> logger,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    /// <summary>Seeds the database with roles, users and clients.</summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Seeding Identity roles...");
        foreach (var role in new[] { Roles.Broker, Roles.Seeker, Roles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                logger.LogInformation("Created role: {Role}", role);
            }
        }

        // Admin created from configuration/secrets
        var adminEmail = configuration["Auth:AdminEmail"]
            ?? throw new InvalidOperationException("Auth:AdminEmail is not configured.");
        var adminPassword = configuration["Auth:AdminPassword"]
            ?? throw new InvalidOperationException("Auth:AdminPassword is not configured.");

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator"
            };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, Roles.Admin);
                logger.LogInformation("Created admin user with Id {UserId}.", admin.Id);
            }
            else
            {
                logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Seed OAuth Clients
        await SeedClientsAsync(ct);

        // Demo users only in Development
        if (environment.IsDevelopment())
        {
            await SeedDemoUsersAsync(ct);
        }
    }

    private async Task SeedClientsAsync(CancellationToken ct)
    {
        logger.LogInformation("Seeding OAuth clients...");

        var clients = new[]
        {
            new {
                Id = "housebroker-web",
                Name = "House Broker Web SPA",
                // Public client: no secret. Enabled in every environment.
                Enabled = true,
                Secret = (string?)null,
                RedirectUris = new[] { "http://localhost:4200/callback", "http://localhost/callback", "http://localhost:80/callback", "http://localhost:8088/callback" },
                // The SPA signs out to its own origin (post_logout_redirect_uri = window.location.origin).
                PostLogoutRedirectUris = new[] { "http://localhost:4200", "http://localhost", "http://localhost:80", "http://localhost:8088" },
                Types = new[] { OpenIddictConstants.ClientTypes.Public },
                Permissions = new[] {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.EndSession,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken
                },
                Scopes = new[] { "openid", "profile", "email", "phone", "offline_access", "listings.read", "contacts.read", "listings.write", "commission.read", "admin.commission" },
            },
            new {
                Id = "housebroker-swagger",
                Name = "House Broker Swagger",
                // Development/Staging only (plan 02). The secret comes from configuration; without it the client is not seeded.
                Enabled = !environment.IsProduction() && !string.IsNullOrWhiteSpace(configuration["Auth:SwaggerClientSecret"]),
                Secret = configuration["Auth:SwaggerClientSecret"],
                // Swagger UI is served under the /api path base (see Program.cs UseSwaggerUI).
                RedirectUris = new[] { "http://localhost:4200/api/swagger/oauth2-redirect.html", "http://localhost:5049/api/swagger/oauth2-redirect.html" },
                PostLogoutRedirectUris = new[] { "http://localhost:4200", "http://localhost:5049" },
                Types = new[] { OpenIddictConstants.ClientTypes.Confidential },
                Permissions = new[] {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken
                },
                Scopes = new[] { "openid", "profile", "email", "phone", "offline_access", "listings.read", "contacts.read", "listings.write", "commission.read", "admin.commission" },
            },
            new {
                Id = "demo-partner",
                Name = "Demo Partner",
                // Development only (plan 02). The secret comes from configuration; without it the client is not seeded.
                Enabled = environment.IsDevelopment() && !string.IsNullOrWhiteSpace(configuration["Auth:DemoPartnerSecret"]),
                Secret = configuration["Auth:DemoPartnerSecret"],
                RedirectUris = Array.Empty<string>(),
                PostLogoutRedirectUris = Array.Empty<string>(),
                Types = new[] { OpenIddictConstants.ClientTypes.Confidential },
                Permissions = new[] {
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials
                },
                Scopes = new[] { "listings.read", "contacts.read" },
            }
        };

        foreach (var c in clients.Where(client => client.Enabled))
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = c.Id,
                DisplayName = c.Name,
                ClientType = c.Types.Contains(OpenIddictConstants.ClientTypes.Public)
                    ? OpenIddictConstants.ClientTypes.Public
                    : OpenIddictConstants.ClientTypes.Confidential,
            };
            foreach (var uri in c.RedirectUris)
            {
                descriptor.RedirectUris.Add(new Uri(uri));
            }
            foreach (var uri in c.PostLogoutRedirectUris)
            {
                descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
            }
            foreach (var permission in c.Permissions)
            {
                descriptor.Permissions.Add(permission);
            }
            foreach (var scope in c.Scopes)
            {
                descriptor.Permissions.Add("scp:" + scope);
            }
            if (!c.Types.Contains(OpenIddictConstants.ClientTypes.Public) &&
                !string.IsNullOrWhiteSpace(c.Secret))
            {
                descriptor.ClientSecret = c.Secret;
            }

            var existing = await applicationManager.FindByClientIdAsync(c.Id, ct);
            if (existing is null)
            {
                await applicationManager.CreateAsync(descriptor, ct);
                logger.LogInformation("Created client: {ClientId}", c.Id);
            }
            else
            {
                await applicationManager.UpdateAsync(existing, descriptor, ct);
                logger.LogInformation("Updated client: {ClientId}", c.Id);
            }
        }
    }

    private async Task SeedDemoUsersAsync(CancellationToken ct)
    {
        // The demo password is a local secret (user-secrets), never a value in source.
        var demoPassword = configuration["Auth:DemoUserPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogWarning("Auth:DemoUserPassword is not set; demo users were not seeded.");
            return;
        }

        var demoUsers = new[]
        {
            (Id: DemoUsers.BrokerOne, Email: "broker1@demo.com", Password: demoPassword, Role: Roles.Broker, Name: "Broker One", Phone: "9800000001"),
            (Id: DemoUsers.BrokerTwo, Email: "broker2@demo.com", Password: demoPassword, Role: Roles.Broker, Name: "Broker Two", Phone: "9800000002"),
            (Id: DemoUsers.Seeker, Email: "seeker@demo.com", Password: demoPassword, Role: Roles.Seeker, Name: "Demo Seeker", Phone: "9800000003"),
        };

        foreach (var (id, email, password, role, name, phone) in demoUsers)
        {
            if (await userManager.FindByEmailAsync(email) is null)
            {
                var user = new ApplicationUser
                {
                    Id = id,
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = name,
                    PhoneNumber = phone
                };
                var result = await userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user, role);
                    logger.LogInformation("Created demo user with Id {UserId} as {Role}", user.Id, role);
                }
            }
        }
    }
}
