using HouseBroker.Api.RateLimiting;
using HouseBroker.Application.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HouseBroker.Api.Controllers;

/// <summary>Shared reference data used by every page.</summary>
[ApiController]
[Route("v{version:apiVersion}")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public sealed class ReferenceDataController(ILookupService lookupService) : ControllerBase
{
    /// <summary>Gets property types.</summary>
    [HttpGet("lookups/property-types")]
    [AllowAnonymous] // Public reference data.
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [Tags("Lookups")]
    [ProducesResponseType<IReadOnlyList<PropertyTypeDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPropertyTypes(CancellationToken cancellationToken)
    {
        var result = await lookupService.GetPropertyTypesAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets features.</summary>
    [HttpGet("lookups/features")]
    [AllowAnonymous] // Public reference data.
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [Tags("Lookups")]
    [ProducesResponseType<IReadOnlyList<FeatureDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeatures(CancellationToken cancellationToken)
    {
        var result = await lookupService.GetFeaturesAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets deployment metadata.</summary>
    [HttpGet("meta")]
    [AllowAnonymous] // Public metadata.
    [Tags("Lookups")]
    [ProducesResponseType<MetaDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMeta(CancellationToken cancellationToken)
    {
        var result = await lookupService.GetMetaAsync(cancellationToken);
        return result.ToActionResult(this);
    }
}
