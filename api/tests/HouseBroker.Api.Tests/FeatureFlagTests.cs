using HouseBroker.Api.Controllers;
using HouseBroker.Api.Filters;
using HouseBroker.Domain.Shared;
using HouseBroker.ServiceDefaults;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Moq;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>Image upload feature flag tests.</summary>
public sealed class FeatureFlagTests
{
    /// <summary>The upload endpoint is gated by the image uploads flag.</summary>
    [Fact]
    public void UploadImage_RequiresImageUploadsFlag()
    {
        var method = typeof(BrokerListingsController).GetMethod(nameof(BrokerListingsController.UploadImage));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true));

        Assert.Equal(FeatureFlags.ImageUploads, ((RequiresFeatureAttribute)attribute).Feature);
    }

    /// <summary>The image delete endpoint is gated by the image uploads flag.</summary>
    [Fact]
    public void DeleteImage_RequiresImageUploadsFlag()
    {
        var method = typeof(BrokerListingsController).GetMethod(nameof(BrokerListingsController.DeleteImage));
        var attribute = Assert.Single(method!.GetCustomAttributes(typeof(RequiresFeatureAttribute), inherit: true));

        Assert.Equal(FeatureFlags.ImageUploads, ((RequiresFeatureAttribute)attribute).Feature);
    }

    /// <summary>A disabled feature returns a 503 problem details response.</summary>
    [Fact]
    public async Task DisabledFeature_Returns503ProblemDetails()
    {
        var nextCalled = false;
        var context = CreateContext(enabled: false);
        var filter = new RequiresFeatureAttribute(FeatureFlags.ImageUploads);

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        Assert.False(nextCalled);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.Equal(ErrorCodes.FeatureDisabled, problem.Extensions["code"]);
    }

    /// <summary>An enabled feature continues the pipeline.</summary>
    [Fact]
    public async Task EnabledFeature_ContinuesPipeline()
    {
        var nextCalled = false;
        var context = CreateContext(enabled: true);
        var filter = new RequiresFeatureAttribute(FeatureFlags.ImageUploads);

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult<ActionExecutedContext>(null!);
        });

        Assert.True(nextCalled);
        Assert.Null(context.Result);
    }

    /// <summary>Service defaults register feature management and read the flag from configuration.</summary>
    [Fact]
    public async Task AddServiceDefaults_RegistersFeatureManagement()
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FeatureManagement:ImageUploads"] = "false",
        });
        builder.AddServiceDefaults();

        using var app = builder.Build();
        var features = app.Services.GetRequiredService<IFeatureManager>();

        Assert.False(await features.IsEnabledAsync(FeatureFlags.ImageUploads));
    }

    private static ActionExecutingContext CreateContext(bool enabled)
    {
        var features = new Mock<IFeatureManagerSnapshot>();
        features.Setup(manager => manager.IsEnabledAsync(FeatureFlags.ImageUploads)).ReturnsAsync(enabled);

        var services = new ServiceCollection();
        services.AddSingleton(features.Object);
        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        return new ActionExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            Array.Empty<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }
}
