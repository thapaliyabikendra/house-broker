using HouseBroker.Infrastructure;
using HouseBroker.ServiceDefaults;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

var pathBase = builder.Configuration["PathBase"] ?? "/api";
app.UsePathBase(pathBase);

app.UseServiceDefaults();

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