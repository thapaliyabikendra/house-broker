using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace HouseBroker.ServiceDefaults;

/// <summary>Service-defaults extensions.</summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>Adds shared service defaults.</summary>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHealthChecks();
        return builder;
    }

    /// <summary>Uses shared service defaults.</summary>
    public static IApplicationBuilder UseServiceDefaults(this IApplicationBuilder app)
    {
        app.UseHealthChecks("/health/live");
        return app;
    }
}
