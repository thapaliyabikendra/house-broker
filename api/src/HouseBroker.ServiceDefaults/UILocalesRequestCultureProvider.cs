using Microsoft.AspNetCore.Localization;

namespace HouseBroker.ServiceDefaults;

/// <summary>Resolves the culture from the OIDC ui_locales parameter.</summary>
public sealed class UILocalesRequestCultureProvider : IRequestCultureProvider
{
    /// <inheritdoc />
    public Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var uiLocales = httpContext.Request.Query["ui_locales"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(uiLocales))
        {
            return Task.FromResult<ProviderCultureResult?>(null);
        }

        var culture = uiLocales.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(culture))
        {
            return Task.FromResult<ProviderCultureResult?>(null);
        }

        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture, culture));
    }
}
