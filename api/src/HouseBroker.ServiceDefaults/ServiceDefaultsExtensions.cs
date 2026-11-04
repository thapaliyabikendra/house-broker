using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using System.Globalization;


namespace HouseBroker.ServiceDefaults;

/// <summary>Service-defaults extensions.</summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>Adds shared service defaults.</summary>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHealthChecks();
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.AddFeatureManagement();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            var cultures = new[] { "en", "ne" };
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

        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (!context.Response.Headers.ContainsKey("X-Content-Type-Options"))
                {
                    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                }

                if (!context.Response.Headers.ContainsKey("X-Frame-Options"))
                {
                    context.Response.Headers["X-Frame-Options"] = "DENY";
                }

                if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
                {
                    context.Response.Headers["Referrer-Policy"] = "no-referrer";
                }

                if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
                {
                    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
                }

                return Task.CompletedTask;
            });

            await next();
        });

        app.UseHealthChecks("/health/live");
        return app;
    }
}
