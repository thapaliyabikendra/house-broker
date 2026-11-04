using System.Net;
using System.Reflection;
using System.Text.Json;
using HouseBroker.Api.Controllers;
using HouseBroker.Api.RateLimiting;
using HouseBroker.Domain.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>Rate limiting policy, configuration, and rejection tests.</summary>
public sealed class RateLimitingTests
{
    /// <summary>Policy defaults match the documented limits.</summary>
    [Fact]
    public void Options_UseDocumentedDefaults()
    {
        var options = new RateLimitingOptions();

        Assert.Equal(100, options.Global.PermitLimit);
        Assert.Equal(60, options.Search.PermitLimit);
        Assert.Equal(30, options.Write.PermitLimit);
        Assert.Equal(10, options.Upload.PermitLimit);
        Assert.Equal(60, options.Global.WindowSeconds);
        Assert.Equal(60, options.Search.WindowSeconds);
        Assert.Equal(60, options.Write.WindowSeconds);
        Assert.Equal(60, options.Upload.WindowSeconds);
    }

    /// <summary>Policy limits bind from configuration while unspecified limits keep their defaults.</summary>
    [Fact]
    public void Options_BindFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Search:PermitLimit"] = "12",
                ["RateLimiting:Search:WindowSeconds"] = "30",
                ["RateLimiting:Upload:PermitLimit"] = "3",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHouseBrokerRateLimiting(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

        Assert.Equal(12, options.Search.PermitLimit);
        Assert.Equal(30, options.Search.WindowSeconds);
        Assert.Equal(3, options.Upload.PermitLimit);
        Assert.Equal(100, options.Global.PermitLimit);
    }

    /// <summary>Search endpoints use the search policy.</summary>
    [Fact]
    public void SearchEndpoints_UseSearchPolicy()
    {
        Assert.Equal(RateLimitPolicies.Search, GetPolicy(typeof(PublicListingsController), nameof(PublicListingsController.Search)));
        Assert.Equal(RateLimitPolicies.Search, GetPolicy(typeof(ReferenceDataController), nameof(ReferenceDataController.GetPropertyTypes)));
        Assert.Equal(RateLimitPolicies.Search, GetPolicy(typeof(ReferenceDataController), nameof(ReferenceDataController.GetFeatures)));
    }

    /// <summary>Listing and commission mutations use the write policy.</summary>
    [Fact]
    public void WriteEndpoints_UseWritePolicy()
    {
        var listingMethods = new[]
        {
            nameof(MyListingsController.Create),
            nameof(MyListingsController.Update),
            nameof(MyListingsController.Delete),
            nameof(MyListingsController.Publish),
            nameof(MyListingsController.MarkSold),
            nameof(MyListingsController.Archive),
        };

        foreach (var method in listingMethods)
        {
            Assert.Equal(RateLimitPolicies.Write, GetPolicy(typeof(MyListingsController), method));
        }

        Assert.Equal(RateLimitPolicies.Write, GetPolicy(typeof(CommissionRatesController), nameof(CommissionRatesController.Create)));
        Assert.Equal(RateLimitPolicies.Write, GetPolicy(typeof(CommissionRatesController), nameof(CommissionRatesController.Update)));
    }

    /// <summary>The image upload endpoint uses the upload policy.</summary>
    [Fact]
    public void UploadEndpoint_UsesUploadPolicy()
    {
        Assert.Equal(RateLimitPolicies.Upload, GetPolicy(typeof(MyListingsController), nameof(MyListingsController.UploadImage)));
    }

    /// <summary>Controllers document the 429 response for every action.</summary>
    [Fact]
    public void Controllers_Document429()
    {
        AssertDocuments429(typeof(MyListingsController));
        AssertDocuments429(typeof(PublicListingsController));
        AssertDocuments429(typeof(ReferenceDataController));
        AssertDocuments429(typeof(CommissionRatesController));
    }

    /// <summary>Requests over the limit return 429 with Retry-After and a ProblemDetails body.</summary>
    [Fact]
    public async Task SearchPolicy_RejectsRequestsOverLimit()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RateLimiting:Search:PermitLimit"] = "2",
            ["RateLimiting:Search:WindowSeconds"] = "60",
        });
        builder.Services.AddHouseBrokerRateLimiting(builder.Configuration);

        await using var app = builder.Build();
        app.UseRateLimiter();
        app.MapGet("/search", () => Results.Ok()).RequireRateLimiting(RateLimitPolicies.Search);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/search")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/search")).StatusCode);

        var response = await client.GetAsync("/search");
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Retry-After", out var retryAfter));
        Assert.NotEmpty(retryAfter);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var problem = document.RootElement;
        Assert.Equal(StatusCodes.Status429TooManyRequests, problem.GetProperty("status").GetInt32());
        Assert.Equal(ErrorCodes.RateLimited, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("correlationId").GetString()));
    }

    private static string? GetPolicy(Type controller, string methodName)
    {
        var method = controller.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Method {controller.Name}.{methodName} was not found.");
        return method.GetCustomAttributes<EnableRateLimitingAttribute>().FirstOrDefault()?.PolicyName;
    }

    private static void AssertDocuments429(Type controller)
    {
        var documented = controller.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
            .Any(attribute => attribute.StatusCode == StatusCodes.Status429TooManyRequests);
        Assert.True(documented, $"{controller.Name} does not document the 429 response.");
    }
}
