using System.Security.Claims;
using HouseBroker.Auth;
using HouseBroker.Auth.Data;
using HouseBroker.Domain.Common;
using HouseBroker.ServiceDefaults;
using OpenIddict.Abstractions;
using OpenIddict.Server;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue("Kestrel:Limits:MaxRequestBodySize", 10 * 1024 * 1024);
});

builder.AddServiceDefaults();
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});
builder.Services.AddHouseBrokerJsonOptions();

var connectionString = builder.Configuration.GetConnectionString("HouseBroker")
    ?? throw new InvalidOperationException("Connection string 'HouseBroker' is not configured.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

builder.Services.AddScoped<AuthDataSeeder>();
builder.Services.AddHostedService<TokenPruningService>();

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.Password.RequiredLength = 10;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<AuthDbContext>())
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetUserInfoEndpointUris("/connect/userinfo")
            .SetEndSessionEndpointUris("/connect/logout")
            .SetIssuer(new Uri(builder.Configuration["Auth:Issuer"] ?? "http://localhost:5000/sso"));

        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.AllowClientCredentialsFlow();
        options.AllowRefreshTokenFlow();

        options.RegisterScopes(
            "openid", "profile", "email", "phone", "offline_access",
            "listings.read", "contacts.read", "listings.write", "commission.read", "admin.commission");

        options.AddEphemeralEncryptionKey();
        options.AddDevelopmentSigningCertificate();

        options.UseAspNetCore();

        options.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(builder =>
        {
            builder.UseInlineHandler(context =>
            {
                var principal = context.Principal;
                if (principal?.Identity is ClaimsIdentity identity)
                {
                    var roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value)
                        .Concat(principal.FindAll("role").Select(claim => claim.Value))
                        .Distinct(StringComparer.Ordinal)
                        .ToList();

                    foreach (var role in roles)
                    {
                        if (!identity.HasClaim("role", role))
                        {
                            identity.AddClaim(new Claim("role", role));
                        }
                    }

                    var permissions = AuthClaimMapper.GetPermissions(roles);

                    foreach (var permission in permissions)
                    {
                        if (!identity.HasClaim("permission", permission))
                        {
                            identity.AddClaim(new Claim("permission", permission));
                        }
                    }
                }

                if (principal is not null)
                {
                    foreach (var claim in principal.Claims)
                    {
                        if (AuthClaimMapper.IsIdentityClaim(claim.Type))
                        {
                            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
                        }
                        else
                        {
                            claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
                        }
                    }
                }

                return ValueTask.CompletedTask;
            });
        });
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// Data Protection keys are stored in the database in production; development uses the ephemeral provider.

var app = builder.Build();

var pathBase = builder.Configuration["PathBase"] ?? "/sso";
app.UsePathBase(pathBase);

app.UseHsts();
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    await next();
});
app.UseServiceDefaults();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

if (args.Contains("migrate", StringComparer.OrdinalIgnoreCase))
{
    using var migrationScope = app.Services.CreateScope();
    var database = migrationScope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await database.Database.MigrateAsync();

    var seeder = migrationScope.ServiceProvider.GetRequiredService<AuthDataSeeder>();
    await seeder.SeedAsync();

    return;
}

if (args.Contains("register-client", StringComparer.OrdinalIgnoreCase))
{
    using var clientScope = app.Services.CreateScope();
    var manager = clientScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
    var clientId = builder.Configuration["Client:Id"] ?? throw new InvalidOperationException("Client:Id is not configured.");
    var clientSecret = builder.Configuration["Client:Secret"];
    var clientType = string.IsNullOrWhiteSpace(clientSecret)
        ? OpenIddictConstants.ClientTypes.Public
        : OpenIddictConstants.ClientTypes.Confidential;
    var redirectUris = builder.Configuration.GetSection("Client:RedirectUris").Get<string[]>() ?? Array.Empty<string>();
    var scopes = builder.Configuration.GetSection("Client:Scopes").Get<string[]>() ?? new[] { "openid", "profile" };

    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = clientId,
        ClientType = clientType,
        DisplayName = builder.Configuration["Client:Name"] ?? clientId,
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

    var existing = await manager.FindByClientIdAsync(clientId);
    if (existing is null)
    {
        await manager.CreateAsync(descriptor);
        Console.WriteLine($"Registered client {clientId}.");
    }
    else
    {
        await manager.UpdateAsync(existing, descriptor);
        Console.WriteLine($"Updated client {clientId}.");
    }

    return;
}

await app.RunAsync();
