using HouseBroker.Api.Localization;
using HouseBroker.Application.Contracts;
using HouseBroker.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace HouseBroker.Api;

/// <summary>Result to action result mapping.</summary>
public static class ResultExtensions
{
    /// <summary>Maps a non-generic result.</summary>
    public static IActionResult ToActionResult(this Result result, ControllerBase controller)
        => result.IsSuccess
            ? controller.NoContent()
            : ToProblemResult(controller, result.Error!);

    /// <summary>Maps a generic result.</summary>
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller, Func<T?, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is null ? controller.Ok(result.Value) : onSuccess(result.Value);
        }

        return ToProblemResult(controller, result.Error!);
    }

    private static IActionResult ToProblemResult(ControllerBase controller, ApplicationError error)
    {
        var statusCode = error.Kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
            ErrorKind.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
            ErrorKind.RateLimited => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status400BadRequest,
        };

        var localizer = controller.HttpContext.RequestServices.GetService<IStringLocalizer<SharedResource>>();
        var message = localizer is null ? error.Code : localizer[error.Code];

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Code,
            Detail = message,
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["correlationId"] = controller.HttpContext.GetCorrelationId();
        if (!string.IsNullOrWhiteSpace(error.Field))
        {
            problem.Extensions["field"] = error.Field;
        }

        if (error.Arguments is not null && error.Arguments.Count > 0)
        {
            problem.Extensions["errors"] = error.Arguments;
        }

        return new ObjectResult(problem) { StatusCode = statusCode };
    }
}
