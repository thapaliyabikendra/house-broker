using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace HouseBroker.ServiceDefaults;

/// <summary>Shared JSON serialization defaults.</summary>
public static class JsonOptionsExtensions
{
    /// <summary>Registers UTC instant converters for MVC and HTTP JSON serialization.</summary>
    public static IServiceCollection AddHouseBrokerJsonOptions(this IServiceCollection services)
    {
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options => Configure(options.JsonSerializerOptions));
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options => Configure(options.SerializerOptions));
        return services;
    }

    private static void Configure(JsonSerializerOptions options)
    {
        if (options.Converters.Any(converter => converter is UtcDateTimeJsonConverter))
        {
            return;
        }

        options.Converters.Add(new UtcDateTimeJsonConverter());
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
    }
}
