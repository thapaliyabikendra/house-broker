using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using HouseBroker.Api.Localization;
using HouseBroker.Domain.Common;
using HouseBroker.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace HouseBroker.Api.RateLimiting;

/// <summary>Registers the API rate limiting policies.</summary>
public static class RateLimitingExtensions
{
    /// <summary>Adds config-backed rate limiting with localized 429 responses.</summary>
    public static IServiceCollection AddHouseBrokerRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .Validate(
                options => IsValid(options.Global) && IsValid(options.Search) && IsValid(options.Write) && IsValid(options.Upload),
                "Rate limit entries must have a positive permit limit and window.")
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/health")
                    ? RateLimitPartition.GetNoLimiter("health")
                    : CreatePartition(context, options => options.Global, RateLimitPolicies.Global, preferUser: true));

            options.AddPolicy(
                RateLimitPolicies.Search,
                context => CreatePartition(context, options => options.Search, RateLimitPolicies.Search, preferUser: false));
            options.AddPolicy(
                RateLimitPolicies.Write,
                context => CreatePartition(context, options => options.Write, RateLimitPolicies.Write, preferUser: true));
            options.AddPolicy(
                RateLimitPolicies.Upload,
                context => CreatePartition(context, options => options.Upload, RateLimitPolicies.Upload, preferUser: true));

            options.OnRejected = OnRejectedAsync;
        });

        return services;
    }

    private static bool IsValid(RateLimitPolicyOptions options)
        => options.PermitLimit > 0 && options.WindowSeconds > 0;

    private static RateLimitPartition<string> CreatePartition(
        HttpContext context,
        Func<RateLimitingOptions, RateLimitPolicyOptions> selector,
        string policy,
        bool preferUser)
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        var policyOptions = selector(settings);
        var partition = GetPartition(context, preferUser);

        return RateLimitPartition.GetSlidingWindowLimiter(
            $"{policy}:{partition.Key}",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = policyOptions.PermitLimit,
                Window = TimeSpan.FromSeconds(policyOptions.WindowSeconds),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }

    private static (string Key, string Type) GetPartition(HttpContext context, bool preferUser)
    {
        if (preferUser && TryGetUserId(context.User, out var userId))
        {
            return ($"user:{userId}", "user");
        }

        var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return ($"ip:{address}", "ip");
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var value = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out userId);
    }

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        var policy = httpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName
            ?? RateLimitPolicies.Global;
        var partitionType = TryGetUserId(httpContext.User, out _) ? "user" : "ip";
        var correlationId = httpContext.GetCorrelationId();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
            httpContext.Items["CorrelationId"] = correlationId;
            if (!httpContext.Response.Headers.ContainsKey("X-Correlation-Id"))
            {
                httpContext.Response.Headers["X-Correlation-Id"] = correlationId;
            }
        }
        var logger = httpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("HouseBroker.RateLimiting");
        logger?.LogWarning(
            "Rate limit hit for policy {Policy} partition {PartitionType} correlation {CorrelationId}.",
            policy,
            partitionType,
            correlationId);

        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var duration)
            ? duration
            : TimeSpan.FromSeconds(60);
        httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);

        var localizer = httpContext.RequestServices.GetService<IStringLocalizer<SharedResource>>();
        var detail = localizer is null ? "A rate limit was exceeded." : localizer[ErrorCodes.RateLimited].Value;

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = ErrorCodes.RateLimited,
            Detail = detail,
        };
        problem.Extensions["code"] = ErrorCodes.RateLimited;
        problem.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
    }
}
