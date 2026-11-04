using HouseBroker.Application.Contracts.Listings;
using HouseBroker.Api.Authorization;
using HouseBroker.Api.Filters;
using HouseBroker.Api.RateLimiting;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Common;
using HouseBroker.ServiceDefaults;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HouseBroker.Api.Controllers;

/// <summary>Broker listing management endpoints.</summary>
/// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/listings")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public sealed class MyListingsController(IListingService listingService, IImageStorage imageStorage) : ControllerBase
{
    /// <summary>Creates a listing.</summary>
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    /// <response code="201">Created.</response>
    /// <response code="400">Bad request.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden.</response>
    /// <response code="409">Conflict.</response>
    [HttpPost]
    [Idempotent]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.MyListingsCreate)]
    [Tags("Listings")]
    [ProducesResponseType<OwnerListingDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
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
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    [HttpPut("{id:guid}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.MyListingsUpdate)]
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
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    [HttpDelete("{id:guid}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.MyListingsDelete)]
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
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    /// <response code="200">OK.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden.</response>
    [HttpGet("mine")]
    [HasPermission(AppPermissions.MyListings)]
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
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    [HttpGet("mine/{id:guid}")]
    [HasPermission(AppPermissions.MyListings)]
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
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    [HttpPost("{id:guid}/images")]
    [RequiresFeature(FeatureFlags.ImageUploads)]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [HasPermission(AppPermissions.MyListingsImagesManage)]
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
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    [RequiresFeature(FeatureFlags.ImageUploads)]
    [HasPermission(AppPermissions.MyListingsImagesManage)]
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
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    [HttpPost("{id:guid}/publish")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.MyListingsUpdate)]
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
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    [HttpPost("{id:guid}/sold")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.MyListingsUpdate)]
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
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    [HttpPost("{id:guid}/archive")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [HasPermission(AppPermissions.MyListingsUpdate)]
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
