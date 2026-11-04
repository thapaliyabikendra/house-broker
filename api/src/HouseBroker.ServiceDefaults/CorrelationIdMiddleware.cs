using System.Text.RegularExpressions;

namespace HouseBroker.ServiceDefaults;

/// <summary>Maintains a sanitized correlation id.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private static readonly Regex ValidCorrelationId = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var incoming = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        var correlationId = incoming is not null && ValidCorrelationId.IsMatch(incoming)
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-Id"] = correlationId;
            if (!context.Response.Headers.ContainsKey("Access-Control-Expose-Headers"))
            {
                context.Response.Headers.Append("Access-Control-Expose-Headers", "X-Correlation-Id");
            }
            return Task.CompletedTask;
        });

        await next(context).ConfigureAwait(false);
    }
}
