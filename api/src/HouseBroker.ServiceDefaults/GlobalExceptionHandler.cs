using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HouseBroker.ServiceDefaults;

/// <summary>Maps exceptions to problem details.</summary>
public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Domain exception {Code}")]
    private static partial void LogDomainException(ILogger logger, Exception exception, string code);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);

    /// <summary>Attempts to handle an exception.</summary>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is HouseBroker.Domain.Common.DomainException domainException)
        {
            LogDomainException(logger, exception, domainException.Code);

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Type = "https://httpstatuses.io/400",
            };
            problemDetails.Extensions["code"] = domainException.Code;
            problemDetails.Extensions["correlationId"] = httpContext.GetCorrelationId();

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken).ConfigureAwait(false);
            return true;
        }

        LogUnhandledException(logger, exception);

        var serverProblem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
        };
        serverProblem.Extensions["correlationId"] = httpContext.GetCorrelationId();

        await httpContext.Response.WriteAsJsonAsync(serverProblem, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
