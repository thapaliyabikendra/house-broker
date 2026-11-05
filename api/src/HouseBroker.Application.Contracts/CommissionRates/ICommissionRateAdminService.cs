namespace HouseBroker.Application.Contracts.CommissionRates;

/// <summary>Commission rate administration service.</summary>
public interface ICommissionRateAdminService
{
    /// <summary>Gets all commission rates.</summary>
    Task<Result<IReadOnlyList<CommissionRateDto>>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Creates a commission rate band.</summary>
    Task<Result<CommissionRateDto>> CreateAsync(CommissionRateRequest request, CancellationToken cancellationToken);

    /// <summary>Updates a commission rate band.</summary>
    Task<Result<CommissionRateDto>> UpdateAsync(Guid id, CommissionRateRequest request, string? ifMatch, CancellationToken cancellationToken);

    /// <summary>Retires a commission rate band (soft delete).</summary>
    Task<Result> RetireAsync(Guid id, string? ifMatch, CancellationToken cancellationToken);

    /// <summary>Reactivates a retired commission rate band.</summary>
    Task<Result<CommissionRateDto>> ReactivateAsync(Guid id, string? ifMatch, CancellationToken cancellationToken);
}
