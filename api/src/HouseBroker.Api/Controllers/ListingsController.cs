using HouseBroker.Api.Authorization;
using HouseBroker.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HouseBroker.Api.Controllers;

/// <summary>Listings controller.</summary>
[ApiController]
[Route("v1/[controller]")]
public sealed class ListingsController(IListingService listingService) : ControllerBase
{
    /// <summary>Gets a listing.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous] // Anonymous listing detail per scope.
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var includeContact = User.HasClaim("permission", "listings:contact:view") && User.HasClaim("scope", "contacts.read");
        var result = await listingService.GetDetailAsync(id, includeContact, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Creates a listing.</summary>
    [HttpPost]
    [HasPermission("listings:create")]
    public async Task<IActionResult> Create([FromBody] CreateListingRequest request, CancellationToken cancellationToken)
    {
        var result = await listingService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }

        return CreatedAtAction(nameof(Get), new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Updates a listing.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission("listings:update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateListingRequest request, CancellationToken cancellationToken)
    {
        var result = await listingService.UpdateAsync(id, request, Request.Headers.IfMatch.ToString(), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Deletes a listing.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission("listings:delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.DeleteAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets owned listings.</summary>
    [HttpGet("mine")]
    [HasPermission("commission:view")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await listingService.GetMineAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets an owned listing.</summary>
    [HttpGet("mine/{id:guid}")]
    [HasPermission("commission:view")]
    public async Task<IActionResult> GetMineById(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.GetOwnerDetailAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Publishes a listing.</summary>
    [HttpPost("{id:guid}/publish")]
    [HasPermission("listings:update")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.PublishAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }
}