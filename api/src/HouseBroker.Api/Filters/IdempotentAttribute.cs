using HouseBroker.Api.Localization;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Common;
using HouseBroker.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.Text.Json;

namespace HouseBroker.Api.Filters;

/// <summary>Makes a POST endpoint idempotent (plan 4.8).</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class IdempotentAttribute : Attribute, IAsyncActionFilter
{
    /// <summary>Seconds a client should wait before retrying a request whose first attempt is still in progress.</summary>
    private const int InProgressRetryAfterSeconds = 1;

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var key = httpContext.Request.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        var services = httpContext.RequestServices;
        var localizer = services.GetService<IStringLocalizer<SharedResource>>();
        if (!IdempotencyKeyRules.IsValid(key))
        {
            context.Result = Problem(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, Localize(localizer, "idempotency.key_invalid"));
            return;
        }

        var idempotencyService = services.GetRequiredService<IIdempotencyService>();
        var currentUser = services.GetRequiredService<ICurrentUserService>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var logger = services.GetService<ILogger<IdempotentAttribute>>() ?? NullLogger<IdempotentAttribute>.Instance;
        var cancellationToken = httpContext.RequestAborted;
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var serializedArguments = JsonSerializer.Serialize(context.ActionArguments
            .Where(argument => argument.Value is not CancellationToken)
            .ToDictionary(argument => argument.Key, argument => argument.Value), jsonOptions);
        var requestHash = idempotencyService.ComputeRequestHash(serializedArguments);

        var scope = currentUser.UserId.HasValue
            ? $"user:{currentUser.UserId.Value}"
            : (string.IsNullOrWhiteSpace(currentUser.ClientId) ? "anonymous" : $"client:{currentUser.ClientId}");

        // The key record and the business change share one transaction, so a failed attempt leaves no key behind.
        await using var transaction = await unitOfWork.BeginTransactionAsync(TransactionIsolationLevel.Serializable, cancellationToken);

        var begin = await idempotencyService.BeginAsync(key, scope, requestHash, cancellationToken);

        if (begin.Status == IdempotencyStatus.Replay)
        {
            await transaction.CommitAsync(cancellationToken);
            httpContext.Response.Headers["Idempotent-Replayed"] = "true";
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
            await transaction.RollbackAsync(cancellationToken);
            context.Result = Problem(context, StatusCodes.Status422UnprocessableEntity, ErrorCodes.IdempotencyConflict, Localize(localizer, ErrorCodes.IdempotencyConflict));
            return;
        }

        if (begin.Status == IdempotencyStatus.InProgress)
        {
            await transaction.RollbackAsync(cancellationToken);
            // The first attempt is still running; tell the client when a retry with the same key is worth making (plan 4.8).
            httpContext.Response.Headers.RetryAfter = InProgressRetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            context.Result = Problem(context, StatusCodes.Status409Conflict, ErrorCodes.IdempotencyInProgress, Localize(localizer, ErrorCodes.IdempotencyInProgress));
            return;
        }

        ActionExecutedContext executed;
        try
        {
            executed = await next();
        }
        catch (Exception)
        {
            await AbandonAsync(idempotencyService, transaction, key, scope, logger, cancellationToken);
            throw;
        }

        // ASP.NET Core hands action exceptions to filters in executed.Exception instead of throwing from next().
        // An unhandled exception, or a missing result, must not commit the business change.
        if (executed.Result is null || (executed.Exception is not null && !executed.ExceptionHandled))
        {
            await AbandonAsync(idempotencyService, transaction, key, scope, logger, cancellationToken);
            return;
        }

        var statusCode = StatusOf(executed.Result);
        if (statusCode < 200 || statusCode >= 300)
        {
            await AbandonAsync(idempotencyService, transaction, key, scope, logger, cancellationToken);
            return;
        }

        try
        {
            var responseBody = executed.Result switch
            {
                ObjectResult objectResult => JsonSerializer.Serialize(objectResult.Value, jsonOptions),
                ContentResult contentResult => contentResult.Content,
                _ => null,
            };

            await idempotencyService.CompleteAsync(key, scope, statusCode, responseBody, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception)
        {
            await AbandonAsync(idempotencyService, transaction, key, scope, logger, cancellationToken);
            throw;
        }
    }

    private static int StatusOf(IActionResult result) => result switch
    {
        ObjectResult objectResult => objectResult.StatusCode ?? StatusCodes.Status200OK,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        ContentResult contentResult => contentResult.StatusCode ?? StatusCodes.Status200OK,
        _ => StatusCodes.Status200OK,
    };

    /// <summary>
    /// Rolls back the business change and frees the key so the client can retry with the same key.
    /// Cleanup failures are logged and never replace the original outcome.
    /// </summary>
    private static async Task AbandonAsync(
        IIdempotencyService idempotencyService,
        ITransactionScope transaction,
        string key,
        string scope,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Rolling back the transaction for idempotency key {IdempotencyKey} failed.", key);
        }

        try
        {
            await idempotencyService.ReleaseAsync(key, scope, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Releasing idempotency key {IdempotencyKey} failed; the key expires after its retention period.", key);
        }
    }

    private static string Localize(IStringLocalizer<SharedResource>? localizer, string key)
        => localizer is null ? key : localizer[key];

    private static ObjectResult Problem(ActionExecutingContext context, int statusCode, string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            // Title carries the localized message like every other ProblemDetails; the stable code stays in "code".
            Title = detail,
            Detail = detail,
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = context.HttpContext.GetCorrelationId();
        return new ObjectResult(problem) { StatusCode = statusCode };
    }
}
