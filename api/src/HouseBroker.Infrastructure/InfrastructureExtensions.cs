using HouseBroker.Application.Contracts;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HouseBroker.Infrastructure;

/// <summary>Infrastructure dependency injection.</summary>
public static class InfrastructureExtensions
{
    /// <summary>Adds infrastructure services.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HouseBroker")
            ?? throw new InvalidOperationException("Connection string 'HouseBroker' is not configured.");

        services.AddScoped<ICurrentUserService, NullCurrentUserService>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString);
            options.AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());

        return services;
    }
}