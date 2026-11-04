using HouseBroker.Api.RateLimiting;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HouseBroker.Api.Controllers;

/// <summary>Public listing browse and detail endpoints.</summary>
[ApiController]
[Route("v1/listings")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public sealed class PublicListingsController(IListingService listingService, IImageStorage imageStorage) : ControllerBase
{
    /// <summary>Searches listings.</summary>
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
    [HttpGet("{id:guid}")]
    [AllowAnonymous] // Anonymous listing detail per scope.
    [Tags("Listings")]
    [ProducesResponseType<ListingDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var hasContactScope = User.HasClaim("scope", Scopes.ContactsRead) || User.FindAll("scp").Any(claim => claim.Value == Scopes.ContactsRead);
        var hasContactPermission = User.HasClaim("permission", AppPermissions.ListingsContactView);
        var includeContact = hasContactScope && (hasContactPermission || !User.HasClaim(claim => claim.Type == "permission"));
        var result = await listingService.GetDetailAsync(id, includeContact, cancellationToken);
        if (result.IsSuccess)
        {
            this.SetETag(result.Value!.Version);
        }

        return result.ToActionResult(this);
    }

    /// <summary>Gets an image.</summary>
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
        return File(result.Value.Stream, result.Value.ContentType);
    }
}
