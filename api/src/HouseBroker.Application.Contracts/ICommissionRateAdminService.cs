namespace HouseBroker.Application.Contracts;

/// <summary>Commission rate administration service.</summary>
public interface ICommissionRateAdminService
{
    /// <summary>Gets all commission rates.</summary>
    Task<Result<IReadOnlyList<CommissionRateDto>>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Creates a commission rate.</summary>
    Task<Result<CommissionRateDto>> CreateAsync(CommissionRateRequest request, CancellationToken cancellationToken);

    /// <summary>Updates a commission rate.</summary>
    Task<Result<CommissionRateDto>> UpdateAsync(Guid id, CommissionRateRequest request, string? ifMatch, CancellationToken cancellationToken);
}
