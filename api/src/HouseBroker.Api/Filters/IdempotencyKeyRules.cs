using System.Text.RegularExpressions;

namespace HouseBroker.Api.Filters;

/// <summary>Format rules for the Idempotency-Key header (plan 4.8).</summary>
public static partial class IdempotencyKeyRules
{
    /// <summary>Returns whether the key is 16–64 characters from [A-Za-z0-9_-].</summary>
    /// <param name="key">The raw header value.</param>
    public static bool IsValid(string key) => KeyPattern().IsMatch(key);

    [GeneratedRegex("^[A-Za-z0-9_-]{16,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();
}
