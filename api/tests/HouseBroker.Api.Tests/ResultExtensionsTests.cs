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
            HttpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().BuildServiceProvider(),
            },
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

    /// <summary>Field errors become a per-field map of code and localized message.</summary>
    [Fact]
    public void ToActionResult_FieldErrors_EmitsPerFieldCodeAndMessage()
    {
        var controller = CreateController();
        var localizer = new Mock<IStringLocalizer<HouseBroker.Api.Localization.SharedResource>>();
        localizer.Setup(instance => instance[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, $"localized:{key}"));
        controller.HttpContext.RequestServices = new ServiceCollection()
            .AddSingleton(localizer.Object)
            .BuildServiceProvider();

        var error = new ApplicationError("validation.failed", ErrorKind.Validation, "Title")
        {
            FieldErrors = [new FieldError("Title", "validation.failed"), new FieldError("FeatureIds[0]", "validation.failed")],
        };
        var result = Result.Failure(error).ToActionResult(controller);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        var errors = Assert.IsType<Dictionary<string, List<FieldErrorDetail>>>(problem.Extensions["errors"]);
        Assert.Equal(["featureIds[0]", "title"], errors.Keys.Order());
        var title = Assert.Single(errors["title"]);
        Assert.Equal("validation.failed", title.Code);
        Assert.Equal("localized:validation.failed", title.Message);
        Assert.Equal("title", problem.Extensions["field"]?.ToString());
    }

    /// <summary>A single-field error without explicit field errors still gets a per-field entry.</summary>
    [Fact]
    public void ToActionResult_SingleFieldError_SynthesizesFieldEntry()
    {
        var controller = CreateController();
        var result = Result.Failure(new ApplicationError("required", ErrorKind.Validation, "Query")).ToActionResult(controller);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        var errors = Assert.IsType<Dictionary<string, List<FieldErrorDetail>>>(problem.Extensions["errors"]);
        Assert.Equal("required", Assert.Single(errors["query"]).Code);
    }

    /// <summary>Errors without a field carry no per-field map.</summary>
    [Fact]
    public void ToActionResult_NonFieldError_OmitsErrorsMap()
    {
        var controller = CreateController();
        var result = Result.Failure(new ApplicationError("conflict", ErrorKind.Conflict)).ToActionResult(controller);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.False(problem.Extensions.ContainsKey("errors"));
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

    /// <summary>Related ids are exposed as relatedIds; the title carries the localized message, not the raw code.</summary>
    [Fact]
    public void ToActionResult_RelatedIds_EmitsRelatedIdsAndLocalizedTitle()
    {
        var controller = CreateController();
        var localizer = new Mock<IStringLocalizer<HouseBroker.Api.Localization.SharedResource>>();
        localizer.Setup(instance => instance[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, $"localized:{key}"));
        controller.HttpContext.RequestServices = new ServiceCollection()
            .AddSingleton(localizer.Object)
            .BuildServiceProvider();

        var tierId = Guid.NewGuid();
        var error = new ApplicationError("conflict", ErrorKind.Conflict) { RelatedIds = [tierId] };
        var result = Result.Failure(error).ToActionResult(controller);

        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("localized:conflict", problem.Title);
        Assert.Equal(new[] { tierId }, Assert.IsType<List<Guid>>(problem.Extensions["relatedIds"]).ToArray());
    }
}
