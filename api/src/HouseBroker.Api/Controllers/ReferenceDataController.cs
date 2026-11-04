using HouseBroker.Api.RateLimiting;
using HouseBroker.Application.Contracts;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HouseBroker.Api.Controllers;

/// <summary>Shared reference data used by every page.</summary>
/// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public sealed class ReferenceDataController(ILookupService lookupService) : ControllerBase
{
    /// <summary>Gets property types.</summary>
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    /// <response code="200">OK.</response>
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
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    /// <response code="200">OK.</response>
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
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    /// <response code="200">OK.</response>
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
