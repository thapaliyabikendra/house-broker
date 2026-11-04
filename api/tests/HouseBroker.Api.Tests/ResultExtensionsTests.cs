using HouseBroker.Application.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>ResultExtensions mapping tests.</summary>
public sealed class ResultExtensionsTests
{
    private sealed class TestController : ControllerBase
    {
    }

    private static TestController CreateController()
    {
        var controller = new TestController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        return controller;
    }

    /// <summary>Generic success maps to 200.</summary>
    [Fact]
    public void ToActionResult_GenericSuccess_ReturnsOk()
    {
        var controller = CreateController();
        var result = Result.Success<string>("value").ToActionResult(controller);

        var objectResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("value", objectResult.Value);
    }

    /// <summary>Non-generic success maps to 204.</summary>
    [Fact]
    public void ToActionResult_NonGenericSuccess_ReturnsNoContent()
    {
        var controller = CreateController();
        var result = Result.Success().ToActionResult(controller);

        Assert.IsType<NoContentResult>(result);
    }

    /// <summary>Failure maps localized ProblemDetails and status codes.</summary>
    [Theory]
    [InlineData(ErrorKind.NotFound, 404)]
    [InlineData(ErrorKind.Forbidden, 403)]
    [InlineData(ErrorKind.Validation, 400)]
    [InlineData(ErrorKind.Conflict, 409)]
    [InlineData(ErrorKind.PreconditionFailed, 412)]
    [InlineData(ErrorKind.PreconditionRequired, 428)]
    public void ToActionResult_Failure_MapsStatusAndMessage(ErrorKind kind, int expectedStatus)
    {
        var controller = CreateController();
        var localizer = new Mock<IStringLocalizer<HouseBroker.Api.Localization.SharedResource>>();
        localizer.Setup(instance => instance[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, $"localized:{key}"));
        controller.HttpContext.RequestServices = new ServiceCollection()
            .AddSingleton(localizer.Object)
            .BuildServiceProvider();

        var error = new ApplicationError("error.code", kind, "Field");
        var result = (kind switch
        {
            ErrorKind.NotFound => Result.Failure(error),
            _ => Result.Failure(error),
        }).ToActionResult(controller);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal($"localized:error.code", problem.Detail);
        Assert.Equal("error.code", problem.Extensions["code"]?.ToString());
    }
}
