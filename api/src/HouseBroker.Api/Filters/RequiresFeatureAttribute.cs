using HouseBroker.Api.Localization;
using HouseBroker.Domain.Shared;
using HouseBroker.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;
using Microsoft.FeatureManagement;

namespace HouseBroker.Api.Filters;

/// <summary>Rejects a request when the named feature flag is disabled.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequiresFeatureAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _feature;

    /// <summary>Initializes the filter for a feature flag.</summary>
    /// <param name="feature">The feature flag name.</param>
    public RequiresFeatureAttribute(string feature)
    {
        _feature = feature;
    }

    /// <summary>Gets the feature flag name.</summary>
    public string Feature => _feature;

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var features = context.HttpContext.RequestServices.GetRequiredService<IFeatureManagerSnapshot>();
        if (await features.IsEnabledAsync(_feature))
        {
            await next();
            return;
        }

        var localizer = context.HttpContext.RequestServices.GetService<IStringLocalizer<SharedResource>>();
        var message = localizer is null ? ErrorCodes.FeatureDisabled : localizer[ErrorCodes.FeatureDisabled];

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = ErrorCodes.FeatureDisabled,
            Detail = message,
        };
        problem.Extensions["code"] = ErrorCodes.FeatureDisabled;
        problem.Extensions["correlationId"] = context.HttpContext.GetCorrelationId();

        context.Result = new ObjectResult(problem) { StatusCode = StatusCodes.Status503ServiceUnavailable };
    }
}
