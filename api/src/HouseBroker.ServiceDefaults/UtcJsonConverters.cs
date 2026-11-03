using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace HouseBroker.ServiceDefaults;

/// <summary>Serializes DateTime values in UTC ISO 8601 form and rejects offset-less input.</summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    private static readonly Regex HasOffset = new("(Z|[+-]\\d{2}:\\d{2})$", RegexOptions.Compiled);

    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a string.");
        }

        var value = reader.GetString() ?? throw new JsonException("Expected a date string.");

        if (!HasOffset.IsMatch(value))
        {
            throw new JsonException("Date-time input must include an offset.");
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            throw new JsonException("Invalid date-time value.");
        }

        return parsed.UtcDateTime;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
}

/// <summary>Serializes DateTimeOffset values in UTC ISO 8601 form and rejects offset-less input.</summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    private static readonly Regex HasOffset = new("(Z|[+-]\\d{2}:\\d{2})$", RegexOptions.Compiled);

    /// <inheritdoc />
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected a string.");
        }

        var value = reader.GetString() ?? throw new JsonException("Expected a date string.");

        if (!HasOffset.IsMatch(value))
        {
            throw new JsonException("Date-time input must include an offset.");
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            throw new JsonException("Invalid date-time value.");
        }

        return parsed.ToUniversalTime();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
}