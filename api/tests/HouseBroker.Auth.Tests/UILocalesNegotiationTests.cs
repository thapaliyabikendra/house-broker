using HouseBroker.ServiceDefaults;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HouseBroker.Auth.Tests;

/// <summary>UI locales negotiation tests.</summary>
public sealed class UILocalesNegotiationTests
{
    /// <summary>Present.</summary>
    [Fact]
    public async Task Present()
    {
        var provider = new UILocalesRequestCultureProvider();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["ui_locales"] = "ne",
        });

        var result = await provider.DetermineProviderCultureResult(httpContext);

        Assert.NotNull(result);
        var culture = result!.Cultures.Cast<object>().First().ToString();
        var uiCulture = result.UICultures.Cast<object>().First().ToString();
        Assert.Equal("ne", culture);
        Assert.Equal("ne", uiCulture);
    }

    /// <summary>Multiple.</summary>
    [Fact]
    public async Task Multiple()
    {
        var provider = new UILocalesRequestCultureProvider();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["ui_locales"] = "fr ne",
        });

        var result = await provider.DetermineProviderCultureResult(httpContext);

        Assert.NotNull(result);
        var culture = result!.Cultures.Cast<object>().First().ToString();
        Assert.Equal("fr", culture);
    }

    /// <summary>Missing.</summary>
    [Fact]
    public async Task Missing()
    {
        var provider = new UILocalesRequestCultureProvider();
        var httpContext = new DefaultHttpContext();

        var result = await provider.DetermineProviderCultureResult(httpContext);

        Assert.Null(result);
    }
}
