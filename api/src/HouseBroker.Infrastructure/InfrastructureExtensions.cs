using HouseBroker.Application;
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

        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            options.AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        var cacheProvider = configuration["Cache:Provider"];
        if (string.Equals(cacheProvider, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = configuration["Cache:Redis:Configuration"]
                    ?? configuration.GetConnectionString("Redis")
                    ?? "localhost:6379";
            });
            services.AddScoped<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddScoped<ICacheService, MemoryCacheService>();
        }

        services.AddScoped<IUnitOfWork>(provider => new CacheInvalidatingUnitOfWork(provider.GetRequiredService<AppDbContext>(), provider.GetRequiredService<ICacheService>()));
        services.AddScoped<IListingRepository, ListingRepository>();
        services.AddScoped<IImageStorage, SqlImageStorage>();
        services.AddScoped<ListingQueries>();
        services.AddScoped<IListingQueries>(provider => new CachedListingQueries(provider.GetRequiredService<ListingQueries>(), provider.GetRequiredService<ICacheService>(), provider.GetRequiredService<IConfiguration>()));
        services.AddScoped<CommissionRateProvider>();
        services.AddScoped<ICommissionRateProvider>(provider => new CachedCommissionRateProvider(provider.GetRequiredService<CommissionRateProvider>(), provider.GetRequiredService<ICacheService>(), provider.GetRequiredService<IConfiguration>()));

        return services;
    }
}
