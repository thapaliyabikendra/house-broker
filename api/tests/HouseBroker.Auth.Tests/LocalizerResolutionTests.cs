using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;
using Xunit;

namespace HouseBroker.Auth.Tests;

/// <summary>Verifies the Auth localizer resolves keys from the embedded .resx files the app ships.</summary>
public class LocalizerResolutionTests
{
    /// <summary>The string localizer used by pages resolves a known key in English.</summary>
    [Fact]
    public void Localizer_resolves_login_title_in_english()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            var factory = new ResourceManagerStringLocalizerFactory(
                Options.Create(new LocalizationOptions()),
                NullLoggerFactory.Instance);
            var localizer = factory.Create(typeof(Resources.Resources));

            var entry = localizer["Login_Title"];

            Assert.False(entry.ResourceNotFound, "Login_Title was not found in the embedded resources.");
            Assert.Equal("Sign In", entry.Value);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
