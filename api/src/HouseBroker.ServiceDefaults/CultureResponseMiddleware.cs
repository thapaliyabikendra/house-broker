using System.Globalization;

namespace HouseBroker.ServiceDefaults;

/// <summary>Adds culture-related response headers.</summary>
public sealed class CultureResponseMiddleware(RequestDelegate next)
{
    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("Vary"))
            {
                context.Response.Headers.Append("Vary", "Accept-Language");
            }

            if (!context.Response.Headers.ContainsKey("Content-Language") &&
                !string.IsNullOrWhiteSpace(CultureInfo.CurrentCulture.Name))
            {
                context.Response.Headers["Content-Language"] = CultureInfo.CurrentCulture.Name;
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
