using HouseBroker.Application.Contracts;
using HouseBroker.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;

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

    private static IActionResult ToProblemResult(ControllerBase controller, Error error)
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

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Code,
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["correlationId"] = controller.HttpContext.GetCorrelationId();

        return new ObjectResult(problem) { StatusCode = statusCode };
    }
}
