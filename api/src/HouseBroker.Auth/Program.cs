using System.Security.Claims;
using HouseBroker.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using HouseBroker.Auth.Data;
using HouseBroker.Domain.Common;
using HouseBroker.ServiceDefaults;
using OpenIddict.Abstractions;
using OpenIddict.Server;

var builder = WebApplication.CreateBuilder(args);

var secretOverrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
foreach (System.Collections.DictionaryEntry variable in Environment.GetEnvironmentVariables())
{
    var name = (string)variable.Key!;
    if (!name.EndsWith("_FILE", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    var path = (string)variable.Value!;
    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
    {
        continue;
    }

    secretOverrides[name[..^5].Replace("__", Microsoft.Extensions.Configuration.ConfigurationPath.KeyDelimiter, StringComparison.Ordinal)] = File.ReadAllText(path).Trim();
}

if (secretOverrides.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(secretOverrides);
}

builder.Services.Configure<Microsoft.Extensions.Hosting.HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue("Kestrel:Limits:MaxRequestBodySize", 10 * 1024 * 1024);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
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

builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.HouseBroker), "A House Broker database connection string is required.")
    .ValidateOnStart();

builder.Services.AddHealthChecks().Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
    "database",
    serviceProvider => new HouseBroker.Auth.HealthChecks.DatabaseHealthCheck(
        connectionString,
        serviceProvider.GetRequiredService<TimeProvider>()),
    failureStatus: null,
    tags: null));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDataProtection();
}
else
{
    builder.Services.AddDataProtection().PersistKeysToDbContext<AuthDbContext>();
}

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
    options.Secure = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

builder.Services.AddScoped<AuthDataSeeder>();
var identityErrorDescriber = builder.Services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(Microsoft.AspNetCore.Identity.IdentityErrorDescriber));
if (identityErrorDescriber is not null)
{
    builder.Services.Remove(identityErrorDescriber);
}
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.IdentityErrorDescriber, HouseBroker.Auth.LocalizedIdentityErrorDescriber>();
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

        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));

        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.AllowClientCredentialsFlow();
        options.AllowRefreshTokenFlow();

        options.RegisterScopes(
            "openid", "profile", "email", "phone", "offline_access",
            "listings.read", "contacts.read", "listings.write", "commission.read", "admin.commission");

        if (builder.Environment.IsDevelopment())
        {
            options.AddEphemeralEncryptionKey();
        }
        else
        {
            var encryptionCertificatePath = builder.Configuration["OpenIddict:EncryptionCertificate:Path"];
            var encryptionCertificatePassword = builder.Configuration["OpenIddict:EncryptionCertificate:Password"];

            if (string.IsNullOrWhiteSpace(encryptionCertificatePath))
            {
                throw new InvalidOperationException("OpenIddict encryption certificate path is not configured for production.");
            }

            var encryptionCertificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                encryptionCertificatePath,
                encryptionCertificatePassword,
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.MachineKeySet);

            options.AddEncryptionCertificate(encryptionCertificate);
        }

        // Development uses an ephemeral signing certificate; production must configure a persistent
        // signing certificate via OpenIddict:SigningCertificate (path + optional password).
        if (builder.Environment.IsDevelopment())
        {
            options.AddDevelopmentSigningCertificate();
        }
        else
        {
            var signingCertificatePath = builder.Configuration["OpenIddict:SigningCertificate:Path"];
            var signingCertificatePassword = builder.Configuration["OpenIddict:SigningCertificate:Password"];

            if (string.IsNullOrWhiteSpace(signingCertificatePath))
            {
                throw new InvalidOperationException("OpenIddict signing certificate path is not configured for production.");
            }

            var signingCertificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
                signingCertificatePath,
                signingCertificatePassword,
                System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.MachineKeySet);

            options.AddSigningCertificate(signingCertificate);
        }

        options.UseAspNetCore().EnableTokenEndpointPassthrough();

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
                    var grantedScopes = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var scopeClaim in principal.FindAll("scp"))
                    {
                        grantedScopes.Add(scopeClaim.Value);
                    }

                    foreach (var scopeClaim in principal.FindAll("scope"))
                    {
                        foreach (var value in scopeClaim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            grantedScopes.Add(value);
                        }
                    }

                    foreach (var claim in principal.Claims)
                    {
                        if (AuthClaimMapper.IsIdentityClaim(claim.Type)
                            && AuthClaimMapper.IsIdentityClaimAllowed(claim.Type, grantedScopes))
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
app.UseHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        if (app.Environment.IsDevelopment())
        {
            await context.Response.WriteAsJsonAsync(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.ToDictionary(
                    entry => entry.Key,
                    entry => new
                    {
                        status = entry.Value.Status.ToString(),
                        description = entry.Value.Description,
                        data = entry.Value.Data
                    })
            });
        }
        else
        {
            await context.Response.WriteAsync($"{{\"status\":\"{report.Status}\"}}");
        }
    }
});
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
    var registrationService = new ClientRegistrationService(
        clientScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>(),
        builder.Configuration);
    await registrationService.RegisterAsync();
    return;
}

await app.RunAsync();


