using HouseBroker.Application;
using HouseBroker.Application.Contracts;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HouseBroker.Infrastructure;

/// <summary>Infrastructure dependency injection.</summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Adds infrastructure services.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HouseBroker")
            ?? throw new InvalidOperationException("Connection string 'HouseBroker' is not configured.");

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.HouseBroker), "A House Broker database connection string is required.")
            .ValidateOnStart();

        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((provider, options) =>
        {
            options.UseSqlServer(connectionString, sql => sql.CommandTimeout(30));
            options.AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddOptions<CacheOptions>()
            .Bind(configuration.GetSection(CacheOptions.SectionName))
            .Validate(
                options =>
                {
                    if (!string.Equals(options.Provider, "Redis", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    return !string.IsNullOrWhiteSpace(options.Redis.Configuration)
                        || !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis"));
                },
                "Redis cache configuration is required when Cache:Provider is Redis.")
            .ValidateOnStart();

        var cacheOptions = configuration.GetSection(CacheOptions.SectionName).Get<CacheOptions>() ?? new CacheOptions();
        var cacheProvider = cacheOptions.Provider;
        if (string.IsNullOrWhiteSpace(cacheProvider))
        {
            cacheProvider = configuration["Cache:Provider"];
        }

        if (string.Equals(cacheProvider, "Redis", StringComparison.OrdinalIgnoreCase))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = !string.IsNullOrWhiteSpace(cacheOptions.Redis.Configuration)
                    ? cacheOptions.Redis.Configuration
                    : configuration.GetConnectionString("Redis") ?? "localhost:6379";
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
