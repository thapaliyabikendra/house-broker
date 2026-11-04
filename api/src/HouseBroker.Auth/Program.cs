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
    // Security header policy: never advertise the server implementation.
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = builder.Configuration.GetValue("Kestrel:Limits:MaxRequestBodySize", 10 * 1024 * 1024);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
});

builder.AddServiceDefaults();
builder.Services.AddRazorPages();
// ServiceDefaults sets ResourcesPath for the API. Auth resources are embedded under their type name
// (HouseBroker.Auth.Resources.Resources), so clear the path for this host.
builder.Services.AddLocalization();
builder.Services.Configure<Microsoft.Extensions.Localization.LocalizationOptions>(options => options.ResourcesPath = string.Empty);
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supported = new[] { "en", "ne" };
    var cultures = supported.Select(value => new System.Globalization.CultureInfo(value)).ToList();
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en");
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.RequestCultureProviders = new List<Microsoft.AspNetCore.Localization.IRequestCultureProvider>
    {
        new HouseBroker.Auth.Localization.UiLocalesRequestCultureProvider()
    }.Concat(options.RequestCultureProviders).ToList();
});
builder.Services.AddControllers();
var authRateLimit = builder.Configuration.GetSection("RateLimiting:Auth");
var authPermitLimit = authRateLimit.GetValue("PermitLimit", 10);
var authWindowSeconds = authRateLimit.GetValue("WindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
    // Rejections are 429 with Retry-After (API rule), not the framework default of 503.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = authWindowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    };

    options.AddPolicy("auth", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
                QueueLimit = 0,
            }));
});
builder.Services.AddHouseBrokerJsonOptions();

var connectionString = builder.Configuration.GetConnectionString("HouseBroker")
    ?? throw new InvalidOperationException("Connection string 'HouseBroker' is not configured.");

builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString, sql => sql.CommandTimeout(30)));

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
    options.Secure = builder.Environment.IsDevelopment()
        ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
        : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
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
        var pathBase = builder.Configuration["PathBase"] ?? "/sso";
        options.SetAuthorizationEndpointUris($"{pathBase}/connect/authorize")
            .SetTokenEndpointUris($"{pathBase}/connect/token")
            .SetUserInfoEndpointUris($"{pathBase}/connect/userinfo")
            .SetEndSessionEndpointUris($"{pathBase}/connect/logout")
            .SetIssuer(new Uri(builder.Configuration["Auth:Issuer"]
                ?? throw new InvalidOperationException("Auth:Issuer must be configured.")));

        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));

        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.AllowClientCredentialsFlow();
        options.AllowRefreshTokenFlow();

        options.RegisterScopes(
            "openid", "profile", "email", "phone", "offline_access",
            "listings.read", "contacts.read", "listings.write", "commission.read", "admin.commission");

        // Resource servers validate access tokens locally against the published JWKS (signing keys only),
        // so access tokens are signed JWTs, not encrypted. Identities set aud=housebroker-api.
        options.DisableAccessTokenEncryption();

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

        // Authorization and token requests are handled by AuthorizationController and TokenController.
        var aspNetCoreBuilder = options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough();
        if (builder.Environment.IsDevelopment())
        {
            aspNetCoreBuilder.DisableTransportSecurityRequirement();
        }

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
// Static files come after the header middleware so css/js responses carry the same headers.
app.UseStaticFiles();
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
app.UseRequestLocalization();
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


