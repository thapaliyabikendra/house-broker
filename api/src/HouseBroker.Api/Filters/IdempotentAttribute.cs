using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Common;
using HouseBroker.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;

namespace HouseBroker.Api.Filters;

/// <summary>Makes a POST endpoint idempotent.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        var localizer = context.HttpContext.RequestServices.GetService<Microsoft.Extensions.Localization.IStringLocalizer<HouseBroker.Api.Localization.SharedResource>>();
        if (key.Length > 128)
        {
            context.Result = validationProblem(context, localizer is null ? "The idempotency key is too long." : localizer["idempotency.key_too_long"]);
            return;
        }

        var services = context.HttpContext.RequestServices;
        var idempotencyService = services.GetRequiredService<IIdempotencyService>();
        var currentUser = services.GetRequiredService<ICurrentUserService>();
        var dbContext = services.GetRequiredService<HouseBroker.Infrastructure.Persistence.AppDbContext>();
        var scope = currentUser.UserId.HasValue
            ? $"user:{currentUser.UserId.Value}"
            : (string.IsNullOrWhiteSpace(currentUser.ClientId) ? "anonymous" : $"client:{currentUser.ClientId}");

        var serializedArguments = JsonSerializer.Serialize(context.ActionArguments);
        var requestHash = HouseBroker.Infrastructure.IdempotencyService.ComputeHash(serializedArguments);

        // Keep idempotency record and business write in one database transaction.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(context.HttpContext.RequestAborted);

        var begin = await idempotencyService.BeginAsync(key, scope, requestHash, context.HttpContext.RequestAborted);

        if (begin.Status == IdempotencyStatus.Replay)
        {
            // Replay does not modify state; commit is not required for the existing record.
            await transaction.CommitAsync(context.HttpContext.RequestAborted);
            context.HttpContext.Response.Headers["Idempotent-Replayed"] = "true";
            context.Result = new ContentResult
            {
                StatusCode = begin.StatusCode ?? StatusCodes.Status200OK,
                Content = begin.ResponseBody,
                ContentType = "application/json",
            };
            return;
        }

        if (begin.Status == IdempotencyStatus.Conflict)
        {
            await transaction.RollbackAsync(context.HttpContext.RequestAborted);
            context.Result = conflictProblem(context, ErrorCodes.IdempotencyConflict, localizer is null ? "The idempotency key was already used with a different request." : localizer[ErrorCodes.IdempotencyConflict]);
            return;
        }

        if (begin.Status == IdempotencyStatus.InProgress)
        {
            await transaction.RollbackAsync(context.HttpContext.RequestAborted);
            context.Result = conflictProblem(context, ErrorCodes.IdempotencyInProgress, localizer is null ? "The idempotency key is already being processed." : localizer[ErrorCodes.IdempotencyInProgress]);
            return;
        }

        var statusCode = StatusCodes.Status200OK;
        string? responseBody = null;
        var completed = false;

        try
        {
            var executed = await next();
            if (executed.Result is null)
            {
                await transaction.CommitAsync(context.HttpContext.RequestAborted);
                return;
            }

            statusCode = executed.Result switch
            {
                ObjectResult objectResult => objectResult.StatusCode ?? StatusCodes.Status200OK,
                StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
                ContentResult contentResult => contentResult.StatusCode ?? StatusCodes.Status200OK,
                _ => StatusCodes.Status200OK,
            };

            if (executed.Result is ObjectResult objectResultValue)
            {
                responseBody = JsonSerializer.Serialize(objectResultValue.Value);
            }
            else if (executed.Result is ContentResult contentResultValue)
            {
                responseBody = contentResultValue.Content;
            }

            if (statusCode >= 200 && statusCode < 300)
            {
                await idempotencyService.CompleteAsync(key, scope, statusCode, responseBody, context.HttpContext.RequestAborted);
                await transaction.CommitAsync(context.HttpContext.RequestAborted);
                completed = true;
            }
            else
            {
                await idempotencyService.ReleaseAsync(key, scope, context.HttpContext.RequestAborted);
                await transaction.RollbackAsync(context.HttpContext.RequestAborted);
            }
        }
        catch
        {
            await idempotencyService.ReleaseAsync(key, scope, context.HttpContext.RequestAborted);
            await transaction.RollbackAsync(context.HttpContext.RequestAborted);
            throw;
        }

        if (!completed && statusCode >= 400)
        {
            await idempotencyService.ReleaseAsync(key, scope, context.HttpContext.RequestAborted);
        }
    }

    private static IActionResult validationProblem(ActionExecutingContext context, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = ErrorCodes.ValidationFailed,
            Detail = detail,
        };
        problem.Extensions["code"] = ErrorCodes.ValidationFailed;
        problem.Extensions["correlationId"] = context.HttpContext.GetCorrelationId();
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status422UnprocessableEntity };
    }

    private static IActionResult conflictProblem(ActionExecutingContext context, string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = code,
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = context.HttpContext.GetCorrelationId();
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
    }
}
