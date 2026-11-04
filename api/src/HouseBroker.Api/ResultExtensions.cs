using System.Text.Json;
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
            // Title is localized like Detail; the stable code stays in the "code" extension.
            Title = message,
            Detail = message,
        };
        problem.Extensions["code"] = error.Code;
        problem.Extensions["correlationId"] = controller.HttpContext.GetCorrelationId();
        if (error.RelatedIds.Count > 0)
        {
            problem.Extensions["relatedIds"] = error.RelatedIds.ToList();
        }

        var fieldErrors = FieldErrorsOf(error);
        if (fieldErrors.Count > 0)
        {
            // The first failing field is also exposed as "field" for single-field callers.
            problem.Extensions["field"] = fieldErrors[0].Field;
            problem.Extensions["errors"] = fieldErrors
                .GroupBy(entry => entry.Field, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(entry => new FieldErrorDetail(entry.Code, Localize(localizer, entry.Code))).ToList(),
                    StringComparer.Ordinal);
        }

        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    /// <summary>Returns the per-field entries (camelCase field names), synthesizing one from <see cref="ApplicationError.Field"/> when needed.</summary>
    private static List<(string Field, string Code)> FieldErrorsOf(ApplicationError error)
    {
        var source = error.FieldErrors.Count > 0
            ? error.FieldErrors
            : string.IsNullOrWhiteSpace(error.Field)
                ? Array.Empty<FieldError>()
                : new[] { new FieldError(error.Field, error.Code) };

        return source
            .Select(entry => (Field: JsonNamingPolicy.CamelCase.ConvertName(entry.Field), entry.Code))
            .ToList();
    }

    private static string Localize(IStringLocalizer<SharedResource>? localizer, string code)
        => localizer is null ? code : localizer[code];
}
