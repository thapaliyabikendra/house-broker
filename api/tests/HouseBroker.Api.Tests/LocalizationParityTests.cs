using System.Collections;
using System.Globalization;
using System.Resources;
using HouseBroker.Api.Localization;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>Verifies API .resx localization parity.</summary>
public class LocalizationParityTests
{
    /// <summary>English and Nepali resource sets expose the same keys.</summary>
    [Fact]
    public void EnAndNeKeysMatch()
    {
        var assembly = typeof(SharedResource).Assembly;
        var manager = new ResourceManager("HouseBroker.Api.Resources.SharedResource", assembly);

        var enKeys = GetKeys(manager, "en");
        var neKeys = GetKeys(manager, "ne");

        Assert.Equal(enKeys.OrderBy(key => key, StringComparer.Ordinal),
            neKeys.OrderBy(key => key, StringComparer.Ordinal));
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
