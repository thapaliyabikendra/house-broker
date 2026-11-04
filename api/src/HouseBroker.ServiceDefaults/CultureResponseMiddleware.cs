using System.Globalization;

namespace HouseBroker.ServiceDefaults;

/// <summary>Adds culture-related response headers.</summary>
public sealed class CultureResponseMiddleware(RequestDelegate next)
{
    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.OnStarting(() =>
        {
            var varyValues = (context.Response.Headers.Vary.ToString() ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!varyValues.Contains("Accept-Language"))
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

        await next(context).ConfigureAwait(false);
    }
}
