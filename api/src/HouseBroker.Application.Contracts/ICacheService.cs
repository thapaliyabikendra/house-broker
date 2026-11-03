namespace HouseBroker.Application.Contracts;

/// <summary>Cache abstraction.</summary>
public interface ICacheService
{
    /// <summary>Gets a cached value.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken);

    /// <summary>Sets a cached value.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration, CancellationToken cancellationToken);

    /// <summary>Removes a cached value.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken);

    /// <summary>Gets or creates a cached value.</summary>
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration, CancellationToken cancellationToken);

    /// <summary>Invalidates all listing cache entries.</summary>
    Task InvalidateListingsAsync(CancellationToken cancellationToken);

    /// <summary>Invalidates commission-rate cache entries.</summary>
    Task InvalidateCommissionRatesAsync(CancellationToken cancellationToken);
}
