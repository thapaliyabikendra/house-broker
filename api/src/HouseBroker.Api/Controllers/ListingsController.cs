using HouseBroker.Api.Authorization;
using HouseBroker.Api.Filters;
using HouseBroker.Application.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace HouseBroker.Api.Controllers;

/// <summary>Listings controller.</summary>
[ApiController]
[Route("v1/[controller]")]
public sealed class ListingsController(IListingService listingService, IImageStorage imageStorage) : ControllerBase
{
    /// <summary>Searches listings.</summary>
    [HttpGet]
    [AllowAnonymous] // Anonymous search per scope.
    public async Task<IActionResult> Search([FromQuery] ListingSearchQuery query, CancellationToken cancellationToken)
    {
        var result = await listingService.SearchAsync(query ?? new ListingSearchQuery(), cancellationToken);
        return result.ToActionResult(this);
    }

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
    [Idempotent]
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

    /// <summary>Uploads an image.</summary>
    [HttpPost("{id:guid}/images")]
    [HasPermission("listings:images:manage")]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest();
        }

        await using var stream = file.OpenReadStream();
        var result = await imageStorage.UploadAsync(id, stream, file.FileName, file.ContentType, file.Length, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets an image.</summary>
    [HttpGet("{id:guid}/images/{imageId:guid}")]
    [AllowAnonymous] // Public listing image.
    public async Task<IActionResult> GetImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var result = await imageStorage.GetAsync(id, imageId, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }

        Response.Headers.ETag = result.Value!.ETag ?? string.Empty;
        Response.Headers.CacheControl = "public, max-age=31536000";
        return File(result.Value.Stream, result.Value.ContentType);
    }

    /// <summary>Deletes an image.</summary>
    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [HasPermission("listings:images:manage")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var result = await imageStorage.DeleteAsync(id, imageId, cancellationToken);
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
