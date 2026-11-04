using System.Security.Claims;
using HouseBroker.Auth.Data;
using HouseBroker.Domain.Shared;
using HouseBroker.ServiceDefaults;
using OpenIddict.Abstractions;
using OpenIddict.Server;
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddRazorPages();
builder.Services.AddControllers();

var connectionString = builder.Configuration.GetConnectionString("HouseBroker")
    ?? throw new InvalidOperationException("Connection string 'HouseBroker' is not configured.");

builder.Services.AddDbContext<AuthDbContext>(options => options.UseSqlServer(connectionString));

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
            .SetEndSessionEndpointUris("/connect/logout");

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
                            permissions.Add(Permissions.ListingsView);
                            permissions.Add(Permissions.ListingsContactView);
                            permissions.Add(Permissions.ListingsCreate);
                            permissions.Add(Permissions.ListingsUpdate);
                            permissions.Add(Permissions.ListingsDelete);
                            permissions.Add(Permissions.ListingsImagesManage);
                            permissions.Add(Permissions.CommissionView);
                        }
                        else if (role == Roles.Seeker)
                        {
                            permissions.Add(Permissions.ListingsView);
                            permissions.Add(Permissions.ListingsContactView);
                        }
                        else if (role == Roles.Admin)
                        {
                            permissions.Add(Permissions.CommissionRatesManage);
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

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

if (args.Contains("migrate", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await database.Database.MigrateAsync();
    return;
}

app.Run();
