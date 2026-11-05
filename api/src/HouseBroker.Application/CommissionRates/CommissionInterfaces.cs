using HouseBroker.Domain;

namespace HouseBroker.Application.CommissionRates;

/// <summary>Commission rate repository.</summary>
public interface ICommissionRateRepository
{
    /// <summary>Gets all commission rates for reading.</summary>
    Task<IReadOnlyList<CommissionRate>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Gets all commission rates as tracked entities for a write inside the current transaction.</summary>
    Task<IReadOnlyList<CommissionRate>> GetAllForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>Gets a commission rate.</summary>
    Task<CommissionRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Adds a commission rate.</summary>
    Task AddAsync(CommissionRate rate, CancellationToken cancellationToken);

    /// <summary>Adds an audit event.</summary>
    Task AddAuditEventAsync(Domain.AuditEvent auditEvent, CancellationToken cancellationToken);
}
