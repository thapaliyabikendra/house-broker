using HouseBroker.Api.Authorization;
using HouseBroker.Api.Filters;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Shared;
using Microsoft.AspNetCore.Mvc;

namespace HouseBroker.Api.Controllers;

/// <summary>Administrator commission rate management endpoints.</summary>
[ApiController]
[Route("v1/admin/commission-rates")]
public sealed class AdminCommissionRatesController(ICommissionRateAdminService service) : ControllerBase
{
    /// <summary>Gets all commission rates.</summary>
    [HttpGet]
    [HasPermission(Permissions.CommissionRatesManage)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await service.GetAllAsync(cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Creates a commission rate.</summary>
    [HttpPost]
    [HasPermission(Permissions.CommissionRatesManage)]
    [Idempotent]
    public async Task<IActionResult> Create([FromBody] CommissionRateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>Updates a commission rate.</summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.CommissionRatesManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CommissionRateRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, request, Request.Headers.IfMatch.ToString(), cancellationToken);
        return result.ToActionResult(this);
    }
}
