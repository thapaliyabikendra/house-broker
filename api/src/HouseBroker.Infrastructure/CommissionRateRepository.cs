using HouseBroker.Application;
using HouseBroker.Domain;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HouseBroker.Infrastructure;

/// <summary>Commission rate repository implementation.</summary>
public sealed class CommissionRateRepository(AppDbContext dbContext) : ICommissionRateRepository
{
    private readonly AppDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<IReadOnlyList<CommissionRate>> GetAllAsync(CancellationToken cancellationToken)
        => _dbContext.CommissionRates.AsNoTracking().OrderBy(rate => rate.MinPrice).ToListAsync(cancellationToken).ContinueWith(task => (IReadOnlyList<CommissionRate>)task.Result, cancellationToken);

    /// <inheritdoc />
    public Task<CommissionRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.CommissionRates.FirstOrDefaultAsync(rate => rate.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(CommissionRate rate, CancellationToken cancellationToken)
        => await _dbContext.CommissionRates.AddAsync(rate, cancellationToken);

    /// <inheritdoc />
    public async Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        => await _dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken);
}
