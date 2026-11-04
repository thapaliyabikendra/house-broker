using System.Text.Json;
using HouseBroker.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>UTC JSON converter tests.</summary>
public sealed class UtcJsonConverterTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    /// <summary>Instants without an offset are rejected.</summary>
    [Theory]
    [InlineData("2026-01-01T00:00:00")]
    [InlineData("2026-01-01T00:00:00.000")]
    public void DateTime_WithoutOffset_IsRejected(string value)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DateTime>($"\"{value}\"", Options));
    }

    /// <summary>Offset input is converted to UTC.</summary>
    [Fact]
    public void DateTime_WithOffset_IsConvertedToUtc()
    {
        var value = JsonSerializer.Deserialize<DateTime>("\"2026-01-01T05:45:00+05:45\"", Options);

        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), value);
    }

    /// <summary>Unspecified values are treated as UTC rather than local time.</summary>
    [Fact]
    public void DateTime_Unspecified_IsSerializedAsUtc()
    {
        var json = JsonSerializer.Serialize(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), Options);

        Assert.Equal("\"2026-01-01T00:00:00.000Z\"", json);
    }

    /// <summary>DateTimeOffset is normalized to UTC on read and write.</summary>
    [Fact]
    public void DateTimeOffset_IsNormalizedToUtc()
    {
        var value = JsonSerializer.Deserialize<DateTimeOffset>("\"2026-01-01T05:45:00+05:45\"", Options);
        var json = JsonSerializer.Serialize(value, Options);

        Assert.Equal(TimeSpan.Zero, value.Offset);
        Assert.Equal("\"2026-01-01T00:00:00.000Z\"", json);
    }

    /// <summary>Both MVC and HTTP JSON options receive the UTC converters.</summary>
    [Fact]
    public void AddHouseBrokerJsonOptions_RegistersConvertersForMvcAndHttp()
    {
        var services = new ServiceCollection();
        services.AddHouseBrokerJsonOptions();
        using var provider = services.BuildServiceProvider();

        var mvc = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value;
        var http = provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value;

        Assert.Contains(mvc.JsonSerializerOptions.Converters, converter => converter is UtcDateTimeJsonConverter);
        Assert.Contains(http.SerializerOptions.Converters, converter => converter is UtcDateTimeJsonConverter);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeJsonConverter());
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());
        return options;
    }
}
