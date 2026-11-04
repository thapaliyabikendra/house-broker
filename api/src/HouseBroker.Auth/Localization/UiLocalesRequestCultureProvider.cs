using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;

namespace HouseBroker.Auth.Localization;

/// <summary>Resolves the request culture from the OpenID Connect <c>ui_locales</c> query parameter.</summary>
public sealed class UiLocalesRequestCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var value = httpContext.Request.Query["ui_locales"].ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.FromResult<ProviderCultureResult?>(null);
        }

        var culture = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(culture))
        {
            return Task.FromResult<ProviderCultureResult?>(null);
        }

        culture = culture.Split('-')[0];
        if (culture.Equals("en", StringComparison.OrdinalIgnoreCase) || culture.Equals("ne", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture, culture));
        }

        return Task.FromResult<ProviderCultureResult?>(null);
    }
}