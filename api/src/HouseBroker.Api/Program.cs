using HouseBroker.Api.Authorization;
using HouseBroker.Application.Contracts;
using HouseBroker.Infrastructure;
using HouseBroker.ServiceDefaults;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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

var pathBase = builder.Configuration["PathBase"] ?? "/api";
app.UsePathBase(pathBase);

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

if (args.Contains("migrate", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<HouseBroker.Infrastructure.Persistence.AppDbContext>();
    await database.Database.MigrateAsync();
    var environment = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
    var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
    await HouseBroker.Infrastructure.DatabaseSeeder.SeedAsync(database, environment, clock, CancellationToken.None);
    return;
}

app.Run();
