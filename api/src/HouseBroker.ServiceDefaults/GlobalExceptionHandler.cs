using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HouseBroker.ServiceDefaults;

/// <summary>Maps exceptions to problem details.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Attempts to handle an exception.</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is Domain.Shared.DomainException domainException)
        {
            logger.LogWarning(exception, "Domain exception {Code}", domainException.Code);

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Type = "https://httpstatuses.io/400",
            };
            problemDetails.Extensions["code"] = domainException.Code;
            problemDetails.Extensions["correlationId"] = httpContext.GetCorrelationId();
            problemDetails.Extensions["errors"] = domainException.Arguments;

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        logger.LogError(exception, "Unhandled exception");

        var serverProblem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
        };
        serverProblem.Extensions["correlationId"] = httpContext.GetCorrelationId();

        await httpContext.Response.WriteAsJsonAsync(serverProblem, cancellationToken);
        return true;
    }
}
