using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using System.Globalization;
using Serilog;
using Asp.Versioning;

namespace HouseBroker.ServiceDefaults;

/// <summary>Service-defaults extensions.</summary>
public static class ServiceDefaultsExtensions
{
    private static readonly string[] SupportedCultureNames = ["en", "ne"];

    /// <summary>Adds shared service defaults.</summary>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHealthChecks();
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.AddFeatureManagement();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        builder.Logging.AddSerilog(new LoggerConfiguration().WriteTo.Console(formatProvider: CultureInfo.InvariantCulture).CreateLogger(), dispose: true);

        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VERSION";
            options.SubstituteApiVersionInUrl = true;
        });

        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            var cultures = SupportedCultureNames;
            var supportedCultures = cultures.Select(culture => new CultureInfo(culture)).ToArray();
            options.DefaultRequestCulture = new RequestCulture("en");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;
            options.RequestCultureProviders = new IRequestCultureProvider[]
            {
                new QueryStringRequestCultureProvider(),
                new CookieRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider(),
            };
        });

        return builder;
    }

    /// <summary>Uses shared service defaults.</summary>
    public static IApplicationBuilder UseServiceDefaults(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseRequestLocalization();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<CultureResponseMiddleware>();

        app.UseSerilogRequestLogging();

        app.UseSecurityHeaders();

        app.UseHealthChecks("/health/live");
        return app;
    }
}
