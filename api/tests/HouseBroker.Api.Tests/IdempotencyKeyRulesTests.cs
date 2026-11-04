using HouseBroker.Api.Filters;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>Idempotency-Key format rules (plan 4.8: 16–64 characters, [A-Za-z0-9_-]).</summary>
public sealed class IdempotencyKeyRulesTests
{
    /// <summary>Keys within the length bounds with allowed characters are accepted.</summary>
    [Theory]
    [InlineData("0123456789abcdef")]
    [InlineData("key_with-dashes_and_digits_1234567890")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void IsValid_AcceptsKeysWithinBoundsAndAllowedCharacters(string key)
    {
        Assert.True(IdempotencyKeyRules.IsValid(key));
    }

    /// <summary>Keys outside the length bounds, or with other characters, are rejected.</summary>
    [Theory]
    [InlineData("short-key")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0")]
    [InlineData("has spaces in it 123")]
    [InlineData("bad.characters.1234567")]
    public void IsValid_RejectsKeysOutsideBoundsOrWithOtherCharacters(string key)
    {
        Assert.False(IdempotencyKeyRules.IsValid(key));
    }
}
