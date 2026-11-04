using HouseBroker.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HouseBroker.Api.Controllers;

/// <summary>Shared reference data used by every page.</summary>
[ApiController]
[Route("v1")]
public sealed class ReferenceDataController(ILookupService lookupService) : ControllerBase
{
    /// <summary>Gets property types.</summary>
    [HttpGet("lookups/property-types")]
    [AllowAnonymous] // Public reference data.
    public async Task<IActionResult> GetPropertyTypes(CancellationToken cancellationToken)
    {
        var result = await lookupService.GetPropertyTypesAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets features.</summary>
    [HttpGet("lookups/features")]
    [AllowAnonymous] // Public reference data.
    public async Task<IActionResult> GetFeatures(CancellationToken cancellationToken)
    {
        var result = await lookupService.GetFeaturesAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets deployment metadata.</summary>
    [HttpGet("meta")]
    [AllowAnonymous] // Public metadata.
    public async Task<IActionResult> GetMeta(CancellationToken cancellationToken)
    {
        var result = await lookupService.GetMetaAsync(cancellationToken);
        return result.ToActionResult(this);
    }
}
