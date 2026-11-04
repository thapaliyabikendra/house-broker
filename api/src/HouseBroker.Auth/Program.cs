using System.Security.Claims;
using HouseBroker.Auth.Data;
using HouseBroker.Domain.Common;
using HouseBroker.ServiceDefaults;
using OpenIddict.Abstractions;
using OpenIddict.Server;

var builder = WebApplication.CreateBuilder(args);

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

                    var permissions = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var role in roles)
                    {
                        if (role == Roles.Broker)
                        {
                            permissions.Add(AppPermissions.ListingsView);
                            permissions.Add(AppPermissions.ListingsContactView);
                            permissions.Add(AppPermissions.ListingsCreate);
                            permissions.Add(AppPermissions.ListingsUpdate);
                            permissions.Add(AppPermissions.ListingsDelete);
                            permissions.Add(AppPermissions.ListingsImagesManage);
                            permissions.Add(AppPermissions.CommissionView);
                        }
                        else if (role == Roles.Seeker)
                        {
                            permissions.Add(AppPermissions.ListingsView);
                            permissions.Add(AppPermissions.ListingsContactView);
                        }
                        else if (role == Roles.Admin)
                        {
                            permissions.Add(AppPermissions.CommissionRatesManage);
                        }
                    }

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
                        switch (claim.Type)
                        {
                            case ClaimTypes.Name:
                            case "name":
                            case ClaimTypes.Email:
                            case "email":
                            case ClaimTypes.MobilePhone:
                            case "phone_number":
                                claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken);
                                break;
                            default:
                                claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
                                break;
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
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await database.Database.MigrateAsync();
    
    var seeder = scope.ServiceProvider.GetRequiredService<AuthDataSeeder>();
    await seeder.SeedAsync();
    
    return;
}

await app.RunAsync();
