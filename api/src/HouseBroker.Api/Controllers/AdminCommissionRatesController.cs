using HouseBroker.Application.Contracts.CommissionRates;
using HouseBroker.Api.Authorization;
using HouseBroker.Api.Filters;
using HouseBroker.Api.RateLimiting;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Common;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HouseBroker.Api.Controllers;

/// <summary>Administrator commission rate management endpoints.</summary>
/// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
[ApiController]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/admin/commission-rates")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
public sealed class AdminCommissionRatesController(ICommissionRateAdminService service) : ControllerBase
{
    /// <summary>Gets all commission rates.</summary>
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    /// <response code="200">OK.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden.</response>
    [HttpGet]
    [HasPermission(AppPermissions.CommissionRatesManage)]
    [Tags("Admin: Commission Rates")]
    [ProducesResponseType<IReadOnlyList<CommissionRateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await service.GetAllAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Creates a commission rate.</summary>
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    /// <response code="200">OK.</response>
    /// <response code="400">Bad request.</response>
    /// <response code="401">Unauthorized.</response>
    /// <response code="403">Forbidden.</response>
    /// <response code="409">Conflict.</response>
    [HttpPost]
    [HasPermission(AppPermissions.CommissionRatesManage)]
    [Idempotent]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [Tags("Admin: Commission Rates")]
    [ProducesResponseType<CommissionRateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CommissionRateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Updates a commission rate.</summary>
    /// <remarks>Executes this endpoint according to the House Broker API contract.</remarks>
    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.CommissionRatesManage)]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    [Tags("Admin: Commission Rates")]
    [ProducesResponseType<CommissionRateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CommissionRateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, Request.Headers.IfMatch.ToString(), cancellationToken);
        return result.ToActionResult(this);
    }
}
