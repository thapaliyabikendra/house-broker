using System.Collections;
using System.Globalization;
using System.Resources;
using ResourcesType = HouseBroker.Auth.Resources.Resources;
using Xunit;

namespace HouseBroker.Auth.Tests;

/// <summary>Verifies Auth .resx localization parity.</summary>
public class AuthLocalizationParityTests
{
    /// <summary>English and Nepali resource sets expose the same keys.</summary>
    [Fact]
    public void EnAndNeKeysMatch()
    {
        var assembly = typeof(ResourcesType).Assembly;
        var manager = new ResourceManager("HouseBroker.Auth.Resources.Resources", assembly);

        var enKeys = GetKeys(manager, "en");
        var neKeys = GetKeys(manager, "ne");

        Assert.Equal(enKeys.OrderBy(key => key, StringComparer.Ordinal),
            neKeys.OrderBy(key => key, StringComparer.Ordinal));
    }

    /// <summary>Both cultures define at least one resource.</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("ne")]
    public void ResourceSetIsNotEmpty(string culture)
    {
        var assembly = typeof(ResourcesType).Assembly;
        var manager = new ResourceManager("HouseBroker.Auth.Resources.Resources", assembly);
        Assert.NotEmpty(GetKeys(manager, culture));
    }

    private static List<string> GetKeys(ResourceManager manager, string culture)
    {
        var set = manager.GetResourceSet(CultureInfo.GetCultureInfo(culture), createIfNotExists: true, tryParents: false)
            ?? throw new InvalidOperationException($"Resource set '{culture}' was not found.");

        var keys = new List<string>();
        foreach (DictionaryEntry entry in set)
        {
            keys.Add(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return keys;
    }
}
