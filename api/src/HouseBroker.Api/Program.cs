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
builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    options.AddDefaultPolicy(policy =>
    {
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

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

var authIssuer = builder.Configuration["Auth:Issuer"] ?? builder.Configuration["Auth:Authority"] ?? "http://localhost:5000/sso";

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
                AuthorizationUrl = new Uri(authIssuer + "/connect/authorize"),
                TokenUrl = new Uri(authIssuer + "/connect/token"),
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

builder.Services.AddHealthChecks()
    .Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
        "database",
        serviceProvider => new HouseBroker.Api.HealthChecks.DatabaseHealthCheck(
            connectionString,
            serviceProvider.GetRequiredService<TimeProvider>()),
        failureStatus: null,
        tags: null));

if (string.Equals(builder.Configuration["Cache:Provider"], "Redis", StringComparison.OrdinalIgnoreCase))
{
    var redisConfiguration = builder.Configuration["Cache:Redis:Configuration"]
        ?? builder.Configuration.GetConnectionString("Redis")
        ?? "localhost:6379";
    builder.Services.AddHealthChecks().Add(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckRegistration(
        "redis",
        _ => new HouseBroker.Api.HealthChecks.RedisHealthCheck(redisConfiguration),
        failureStatus: null,
        tags: null));
}

builder.Services.AddInfrastructure(builder.Configuration);

var issuer = builder.Configuration["Auth:Issuer"] ?? builder.Configuration["Auth:Authority"] ?? "http://localhost:5000/sso";
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
app.UseCors();

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


