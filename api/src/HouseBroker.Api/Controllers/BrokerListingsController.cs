using HouseBroker.Api.Authorization;
using HouseBroker.Api.Filters;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Shared;
using Microsoft.AspNetCore.Mvc;

namespace HouseBroker.Api.Controllers;

/// <summary>Broker listing management endpoints.</summary>
[ApiController]
[Route("v1/listings")]
public sealed class BrokerListingsController(IListingService listingService, IImageStorage imageStorage) : ControllerBase
{
    /// <summary>Creates a listing.</summary>
    [HttpPost]
    [Idempotent]
    [HasPermission(Permissions.ListingsCreate)]
    public async Task<IActionResult> Create([FromBody] CreateListingRequest request, CancellationToken cancellationToken)
    {
        var result = await listingService.CreateAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }

        return CreatedAtAction(nameof(PublicListingsController.Get), "PublicListings", new { id = result.Value!.Id }, result.Value);
    }

    /// <summary>Updates a listing.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.ListingsUpdate)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateListingRequest request, CancellationToken cancellationToken)
    {
        var result = await listingService.UpdateAsync(id, request, Request.Headers.IfMatch.ToString(), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Deletes a listing.</summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.ListingsDelete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.DeleteAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets owned listings.</summary>
    [HttpGet("mine")]
    [HasPermission(Permissions.CommissionView)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await listingService.GetMineAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets an owned listing.</summary>
    [HttpGet("mine/{id:guid}")]
    [HasPermission(Permissions.CommissionView)]
    public async Task<IActionResult> GetMineById(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.GetOwnerDetailAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Uploads an image.</summary>
    [HttpPost("{id:guid}/images")]
    [HasPermission(Permissions.ListingsImagesManage)]
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

    /// <summary>Deletes an image.</summary>
    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [HasPermission(Permissions.ListingsImagesManage)]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var result = await imageStorage.DeleteAsync(id, imageId, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Publishes a listing.</summary>
    [HttpPost("{id:guid}/publish")]
    [HasPermission(Permissions.ListingsUpdate)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.PublishAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Marks a listing as sold.</summary>
    [HttpPost("{id:guid}/sold")]
    [HasPermission(Permissions.ListingsUpdate)]
    public async Task<IActionResult> MarkSold(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.MarkSoldAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Archives a listing.</summary>
    [HttpPost("{id:guid}/archive")]
    [HasPermission(Permissions.ListingsUpdate)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.ArchiveAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }
}
