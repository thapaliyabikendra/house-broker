namespace HouseBroker.ServiceDefaults;

/// <summary>Shared extension methods.</summary>
public static class HttpContextExtensions
{
    /// <summary>Gets the correlation id for the request.</summary>
    public static string GetCorrelationId(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Items.TryGetValue("CorrelationId", out var value) && value is string id
            ? id
            : string.Empty;
    }
}
