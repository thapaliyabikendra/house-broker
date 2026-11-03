using HouseBroker.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HouseBroker.Api.Controllers;

/// <summary>Lookup controller.</summary>
[ApiController]
[Route("v1/[controller]")]
public sealed class LookupsController(ILookupService lookupService) : ControllerBase
{
    /// <summary>Gets property types.</summary>
    [HttpGet("property-types")]
    [AllowAnonymous] // Public reference data.
    public async Task<IActionResult> GetPropertyTypes(CancellationToken cancellationToken)
    {
        var result = await lookupService.GetPropertyTypesAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets features.</summary>
    [HttpGet("features")]
    [AllowAnonymous] // Public reference data.
    public async Task<IActionResult> GetFeatures(CancellationToken cancellationToken)
    {
        var result = await lookupService.GetFeaturesAsync(cancellationToken);
        return result.ToActionResult(this);
    }
}
