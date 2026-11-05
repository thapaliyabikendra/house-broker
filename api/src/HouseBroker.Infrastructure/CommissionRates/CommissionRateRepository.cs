using HouseBroker.Application.CommissionRates;
using HouseBroker.Domain;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HouseBroker.Infrastructure.CommissionRates;

/// <summary>Commission rate repository implementation.</summary>
public sealed class CommissionRateRepository(AppDbContext dbContext) : ICommissionRateRepository
{
    private readonly AppDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommissionRate>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await Ordered(_dbContext.CommissionRates.AsNoTracking()).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommissionRate>> GetAllForUpdateAsync(CancellationToken cancellationToken)
    {
        return await Ordered(_dbContext.CommissionRates).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<CommissionRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => _dbContext.CommissionRates.FirstOrDefaultAsync(rate => rate.Id == id, cancellationToken);

    private static IQueryable<CommissionRate> Ordered(IQueryable<CommissionRate> query)
        => query
            .OrderBy(rate => rate.MaxPrice == null)
            .ThenBy(rate => rate.MaxPrice);

    /// <inheritdoc />
    public async Task AddAsync(CommissionRate rate, CancellationToken cancellationToken)
        => await _dbContext.CommissionRates.AddAsync(rate, cancellationToken);

    /// <inheritdoc />
    public async Task AddAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
        => await _dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken);
}
