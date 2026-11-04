using HouseBroker.Application.Contracts.Listings;
using HouseBroker.Api.RateLimiting;
using HouseBroker.Application.Contracts;
using Asp.Versioning;
using HouseBroker.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HouseBroker.Api.Controllers;

/// <summary>Public listing browse and detail endpoints.</summary>
/// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/listings")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public sealed class PublicListingsController(IListingService listingService, IImageStorage imageStorage) : ControllerBase
{
    /// <summary>Searches listings.</summary>
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    /// <response code="200">OK.</response>
    /// <response code="400">Bad request.</response>
    [HttpGet]
    [AllowAnonymous] // Anonymous search per scope.
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [Tags("Listings")]
    [ProducesResponseType<PagedResult<ListingSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] ListingSearchQuery query, CancellationToken cancellationToken)
    {
        var result = await listingService.SearchAsync(query ?? new ListingSearchQuery(), cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Gets a listing.</summary>
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    [HttpGet("{id:guid}")]
    [AllowAnonymous] // Public detail: listing data plus broker contact (raw R13); bulk reads are limited by the search policy.
    [EnableRateLimiting(RateLimitPolicies.Search)]
    [Tags("Listings")]
    [ProducesResponseType<ListingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await listingService.GetDetailAsync(id, cancellationToken);
        if (result.IsSuccess)
        {
            this.SetETag(result.Value!.Version);
            var etag = Response.Headers.ETag.ToString();
            var ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
            if (!string.IsNullOrWhiteSpace(etag) && ifNoneMatch == etag)
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }
        }

        return result.ToActionResult(this);
    }

    /// <summary>Gets an image.</summary>
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    [HttpGet("{id:guid}/images/{imageId:guid}")]
    [AllowAnonymous] // Public listing image.
    [Tags("Images")]
    [Produces("image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(Guid id, Guid imageId, CancellationToken cancellationToken)
    {
        var result = await imageStorage.GetAsync(id, imageId, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ToActionResult(this);
        }

        Response.Headers.ETag = result.Value!.ETag ?? string.Empty;
        Response.Headers.CacheControl = "public, max-age=31536000";
        var ifNoneMatch = Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrWhiteSpace(result.Value.ETag) && ifNoneMatch == (result.Value.ETag ?? string.Empty))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }
        return File(result.Value.Stream, result.Value.ContentType);
    }
}
