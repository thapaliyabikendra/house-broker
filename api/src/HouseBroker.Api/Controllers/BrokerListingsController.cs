using HouseBroker.Api.Authorization;
using HouseBroker.Api.Filters;
using HouseBroker.Api.RateLimiting;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Common;
using HouseBroker.ServiceDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HouseBroker.Api.Controllers;

/// <summary>Broker listing management endpoints.</summary>
[ApiController]
[Route("v1/listings")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public sealed class BrokerListingsController(IListingService listingService, IImageStorage imageStorage) : ControllerBase
{
    /// <summary>Creates a listing.</summary>
    [HttpPost]
    [Idempotent]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.ListingsCreate)]
    [Tags("Listings")]
    [ProducesResponseType<OwnerListingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
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
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.ListingsUpdate)]
    [Tags("Listings")]
    [ProducesResponseType<OwnerListingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateListingRequest request, CancellationToken cancellationToken)
    {
        var result = await listingService.UpdateAsync(id, request, Request.Headers.IfMatch.ToString(), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Deletes a listing.</summary>
    [HttpDelete("{id:guid}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.ListingsDelete)]
    [Tags("Listings")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.DeleteAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets owned listings.</summary>
    [HttpGet("mine")]
    [HasPermission(AppPermissions.CommissionView)]
    [Tags("Listings")]
    [ProducesResponseType<IReadOnlyList<OwnerListingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var result = await listingService.GetMineAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets an owned listing.</summary>
    [HttpGet("mine/{id:guid}")]
    [HasPermission(AppPermissions.CommissionView)]
    [Tags("Listings")]
    [ProducesResponseType<OwnerListingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMineById(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.GetOwnerDetailAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            this.SetETag(result.Value!.Version);
        }

        return result.ToActionResult(this);
    }

    /// <summary>Uploads an image.</summary>
    [HttpPost("{id:guid}/images")]
    [RequiresFeature(FeatureFlags.ImageUploads)]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [HasPermission(AppPermissions.ListingsImagesManage)]
    [Tags("Images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ListingImageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
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
    [RequiresFeature(FeatureFlags.ImageUploads)]
    [HasPermission(AppPermissions.ListingsImagesManage)]
    [Tags("Images")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var result = await imageStorage.DeleteAsync(id, imageId, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Publishes a listing.</summary>
    [HttpPost("{id:guid}/publish")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.ListingsUpdate)]
    [Tags("Listings")]
    [ProducesResponseType<OwnerListingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.PublishAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Marks a listing as sold.</summary>
    [HttpPost("{id:guid}/sold")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.ListingsUpdate)]
    [Tags("Listings")]
    [ProducesResponseType<OwnerListingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkSold(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.MarkSoldAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Archives a listing.</summary>
    [HttpPost("{id:guid}/archive")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.ListingsUpdate)]
    [Tags("Listings")]
    [ProducesResponseType<OwnerListingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.ArchiveAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }
}
