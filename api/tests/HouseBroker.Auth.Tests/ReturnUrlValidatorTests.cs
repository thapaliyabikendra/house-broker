using Xunit;

namespace HouseBroker.Auth.Tests;

/// <summary>Return URL validator tests.</summary>
public sealed class ReturnUrlValidatorTests
{
    /// <summary>Local path accepted.</summary>
    [Fact]
    public void IsLocalUrl_LocalPath_ReturnsTrue()
        => Assert.True(ReturnUrlValidator.IsLocalUrl("/listings"));

    /// <summary>External URL rejected.</summary>
    [Fact]
    public void IsLocalUrl_ExternalUrl_ReturnsFalse()
        => Assert.False(ReturnUrlValidator.IsLocalUrl("https://example.com/steal"));

    /// <summary>Protocol-relative rejected.</summary>
    [Fact]
    public void IsLocalUrl_ProtocolRelative_ReturnsFalse()
        => Assert.False(ReturnUrlValidator.IsLocalUrl("//example.com/steal"));

    /// <summary>Empty rejected.</summary>
    [Fact]
    public void IsLocalUrl_Empty_ReturnsFalse()
        => Assert.False(ReturnUrlValidator.IsLocalUrl(null));
}
