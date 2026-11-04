using Microsoft.AspNetCore.Identity;
using HouseBroker.Domain.Shared;
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
    IConfiguration configuration)
{
    /// <summary>Seeds the database with roles, users and clients.</summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Seeding Identity roles...");
        foreach (var role in new[] { Roles.Broker, Roles.Seeker, Roles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role), ct);
                logger.LogInformation("Created role: {Role}", role);
            }
        }

        // Admin created from configuration/secrets
        var adminEmail = configuration["Auth:AdminEmail"] ?? "admin@housebroker.com";
        var adminPassword = configuration["Auth:AdminPassword"] ?? "DefaultAdminPassword123!";
        
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
                logger.LogInformation("Created admin user: {Email}", adminEmail);
            }
            else
            {
                logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Seed OAuth Clients
        await SeedClientsAsync(ct);

        // Demo users only in Development
        if (configuration.GetValue<string>("Environment") == "Development")
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
                Secret = "web_secret_123", 
                RedirectUris = new[] { "http://localhost:4200/callback" }, 
                Types = new[] { OpenIddictConstants.ClientTypes.Public },
                Permissions = new[] { 
                    OpenIddictConstants.Permissions.Endpoints.Authorization, 
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken
                } 
            },
            new { 
                Id = "housebroker-swagger", 
                Name = "House Broker Swagger", 
                Secret = "swagger_secret_123", 
                RedirectUris = new[] { "http://localhost:5000/swagger/oauth2-redirect.html" }, 
                Types = new[] { OpenIddictConstants.ClientTypes.Confidential },
                Permissions = new[] { 
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken
                } 
            },
            new { 
                Id = "demo-partner", 
                Name = "Demo Partner", 
                Secret = "partner_secret_123", 
                RedirectUris = Array.Empty<string>(), 
                Types = new[] { OpenIddictConstants.ClientTypes.Confidential },
                Permissions = new[] { 
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials
                } 
            }
        };

        foreach (var c in clients)
        {
            if (await applicationManager.FindByClientIdAsync(c.Id) is null)
            {
                var application = new OpenIddictEntityFrameworkCoreApplication
                {
                    ClientId = c.Id,
                    ClientSecret = c.Secret,
                    DisplayName = c.Name,
                    RedirectUris = c.RedirectUris,
                    Permissions = c.Permissions
                };
                await applicationManager.CreateAsync(application, ct);
                logger.LogInformation("Created client: {ClientId}", c.Id);
            }
        }
    }

    private async Task SeedDemoUsersAsync(CancellationToken ct)
    {
        var demoUsers = new[]
        {
            (Email: "broker1@demo.com", Password: "Password123!", Role: Roles.Broker, Name: "Broker One", Phone: "9800000001"),
            (Email: "broker2@demo.com", Password: "Password123!", Role: Roles.Broker, Name: "Broker Two", Phone: "9800000002"),
            (Email: "seeker@demo.com", Password: "Password123!", Role: Roles.Seeker, Name: "Demo Seeker", Phone: "9800000003"),
        };

        foreach (var (email, password, role, name, phone) in demoUsers)
        {
            if (await userManager.FindByEmailAsync(email) is null)
            {
                var user = new ApplicationUser
                {
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
                    logger.LogInformation("Created demo user {Email} as {Role}", email, role);
                }
            }
        }
    }
}