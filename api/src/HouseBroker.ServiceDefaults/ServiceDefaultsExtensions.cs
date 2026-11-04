using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
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
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.AddFeatureManagement();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        var serilogLogger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationId()
            .Enrich.WithEnvironmentName()
            .CreateLogger();
        builder.Logging.AddSerilog(serilogLogger, dispose: true);
        // Register Serilog with DI so request logging can resolve DiagnosticContext.
        builder.Services.AddSerilog(serilogLogger, dispose: false);

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
                new UILocalesRequestCultureProvider(),
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
        app.UseForwardedHeaders();

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("CorrelationId", httpContext.GetCorrelationId());
                var userId = httpContext.User.FindFirst("sub")?.Value ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    diagnosticContext.Set("UserId", userId);
                }

                var role = httpContext.User.FindFirst("role")?.Value ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                if (!string.IsNullOrWhiteSpace(role))
                {
                    diagnosticContext.Set("UserRole", role);
                }

                var clientId = httpContext.User.FindFirst("client_id")?.Value;
                if (!string.IsNullOrWhiteSpace(clientId))
                {
                    diagnosticContext.Set("ClientId", clientId);
                }

                if (httpContext.Connection.RemoteIpAddress is not null)
                {
                    diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress.ToString());
                }
            };
        });

        app.UseSecurityHeaders(options =>
        {
            options.AddFrameOptionsDeny();
            options.AddContentTypeOptionsNoSniff();
            options.AddXssProtectionBlock();
            options.AddReferrerPolicyStrictOriginWhenCrossOrigin();
        });

        app.UseHealthChecks("/health/live");
        return app;
    }
}
