using HouseBroker.Application;
using HouseBroker.Domain;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HouseBroker.Infrastructure;

/// <summary>Commission rate provider implementation.</summary>
public sealed class CommissionRateProvider(AppDbContext dbContext) : ICommissionRateProvider
{
    private readonly AppDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public Task<IReadOnlyList<CommissionRate>> GetActiveAsync(DateTime effectiveAtUtc, CancellationToken cancellationToken)
        => _dbContext.CommissionRates
            .AsNoTracking()
            .Where(rate => rate.IsActive && rate.EffectiveFrom <= effectiveAtUtc)
            .ToListAsync(cancellationToken)
            .ContinueWith(task => (IReadOnlyList<CommissionRate>)task.Result, cancellationToken);
}