using HouseBroker.Api;
using HouseBroker.Api.Authorization;
using HouseBroker.Api.RateLimiting;
using HouseBroker.Application.Contracts;
using HouseBroker.Infrastructure;
using HouseBroker.ServiceDefaults;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.SwaggerGen;
using Asp.Versioning;
using Microsoft.OpenApi;
using System.Reflection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IListingService, HouseBroker.Application.Services.ListingService>();
builder.Services.AddScoped<IValidator<HouseBroker.Application.Contracts.CreateListingRequest>, HouseBroker.Application.Services.CreateListingRequestValidator>();
builder.Services.AddScoped<IValidator<HouseBroker.Application.Contracts.UpdateListingRequest>, HouseBroker.Application.Services.UpdateListingRequestValidator>();
builder.Services.AddScoped<IIdempotencyService, HouseBroker.Infrastructure.IdempotencyService>();
builder.Services.AddScoped<HouseBroker.Application.ICommissionRateRepository, HouseBroker.Infrastructure.CommissionRateRepository>();
builder.Services.AddScoped<ICommissionRateAdminService, HouseBroker.Application.Services.CommissionRateAdminService>();
builder.Services.AddHostedService<HouseBroker.Infrastructure.IdempotencyCleanupService>();
builder.Services.AddScoped<ILookupService, HouseBroker.Application.Services.LookupService>();
builder.Services.AddControllers();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddMvc().AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
builder.Services.AddHouseBrokerJsonOptions();
builder.Services.AddHouseBrokerRateLimiting(builder.Configuration);
builder.Services.AddHouseBrokerOpenApi();

// Swagger UI Configuration
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "House Broker API", 
        Version = "v1" 
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT access token issued by the House Broker identity provider."
    });

    // Security requirement is attached to endpoints individually where applicable.

    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri((builder.Configuration["Auth:Authority"] ?? "http://localhost:5000/sso") + "/connect/authorize"),
                TokenUrl = new Uri((builder.Configuration["Auth:Authority"] ?? "http://localhost:5000/sso") + "/connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    ["openid"] = "OpenID Connect",
                    ["profile"] = "User profile",
                    ["listings.read"] = "Read listings",
                    ["contacts.read"] = "Read broker contact",
                    ["listings.write"] = "Manage listings",
                    ["commission.read"] = "Read commission",
                    ["admin.commission"] = "Manage commission rates",
                },
            },
        },
        Description = "OAuth 2.0 authorization code flow with PKCE via the House Broker identity provider.",
    });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var connectionString = builder.Configuration.GetConnectionString("HouseBroker");
if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = "Server=localhost,1433;Database=HouseBroker;User Id=sa;Password=Your_password123;TrustServerCertificate=True";
}

builder.Services.AddInfrastructure(builder.Configuration);

var issuer = builder.Configuration["Auth:Authority"] ?? "http://localhost:5000/sso";
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = issuer;
        options.Audience = "housebroker-api";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddSingleton<IAuthorizationPolicyProvider>(provider =>
{
    var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Authorization.AuthorizationOptions>>();
    return new PermissionAuthorizationPolicyProvider(new Microsoft.AspNetCore.Authorization.DefaultAuthorizationPolicyProvider(options));
});

builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, ResourceOwnerAuthorizationHandler>();

var app = builder.Build();

app.UseForwardedHeaders();
var pathBase = builder.Configuration["PathBase"] ?? "/api";
app.UsePathBase(pathBase);

app.UseServiceDefaults();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/api/swagger/v1/swagger.json", "House Broker API v1");
        options.OAuthClientId("housebroker-swagger");
        options.OAuthUsePkce();
        options.OAuthScopes("openid", "profile", "listings.read", "contacts.read", "listings.write", "commission.read", "admin.commission");
    });
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.MapOpenApi().AllowAnonymous();

if (args.Contains("migrate", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<HouseBroker.Infrastructure.Persistence.AppDbContext>();
    await database.Database.MigrateAsync();
    var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
    await HouseBroker.Infrastructure.DatabaseSeeder.SeedAsync(database, environment, clock, CancellationToken.None);
    return;
}

await app.RunAsync();
