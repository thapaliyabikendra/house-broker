using HouseBroker.Application.Contracts;

namespace HouseBroker.Infrastructure;

/// <summary>Invalidates listing cache after successful persistence.</summary>
public sealed class CacheInvalidatingUnitOfWork(IUnitOfWork inner, ICacheService cache) : IUnitOfWork
{
    private readonly IUnitOfWork _inner = inner;
    private readonly ICacheService _cache = cache;

    /// <inheritdoc />
    public async Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _inner.SaveChangesAsync(cancellationToken);
        if (result.IsSuccess)
        {
            await _cache.InvalidateListingsAsync(cancellationToken);
        }

        return result;
    }
}
