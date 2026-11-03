using HouseBroker.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HouseBroker.Api.Controllers;

/// <summary>Metadata controller.</summary>
[ApiController]
[Route("v1/[controller]")]
public sealed class MetaController(ILookupService lookupService) : ControllerBase
{
    /// <summary>Gets deployment metadata.</summary>
    [HttpGet]
    [AllowAnonymous] // Public metadata.
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await lookupService.GetMetaAsync(cancellationToken);
        return result.ToActionResult(this);
    }
}
