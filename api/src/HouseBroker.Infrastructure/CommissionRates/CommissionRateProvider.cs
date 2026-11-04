using HouseBroker.Application.Abstractions;
using HouseBroker.Domain;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HouseBroker.Infrastructure.CommissionRates;

/// <summary>Commission rate provider implementation.</summary>
public sealed class CommissionRateProvider(AppDbContext dbContext) : ICommissionRateProvider
{
    private readonly AppDbContext _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CommissionRate>> GetActiveAsync(DateTime effectiveAtUtc, CancellationToken cancellationToken)
    {
        return await _dbContext.CommissionRates
            .AsNoTracking()
            .Where(rate => rate.IsActive && rate.EffectiveFrom <= effectiveAtUtc)
            .ToListAsync(cancellationToken);
    }
}
