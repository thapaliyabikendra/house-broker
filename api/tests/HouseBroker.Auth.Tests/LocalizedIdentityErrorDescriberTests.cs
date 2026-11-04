using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace HouseBroker.Auth.Tests;

/// <summary>Localized identity error describer tests.</summary>
public sealed class LocalizedIdentityErrorDescriberTests
{
    /// <summary>Default error uses localizer.</summary>
    [Fact]
    public void DefaultError_UsesLocalizer()
    {
        var localizer = new Mock<IStringLocalizer<HouseBroker.Auth.Resources.Resources>>();
        localizer.Setup(instance => instance["Identity_DefaultError"]).Returns(new LocalizedString("Identity_DefaultError", "localized default"));

        var describer = new HouseBroker.Auth.LocalizedIdentityErrorDescriber(localizer.Object);

        Assert.Equal("localized default", describer.DefaultError().Description);
    }
}
