namespace HouseBroker.Application.Contracts;

/// <summary>Coordinates persistence.</summary>
public interface IUnitOfWork
{
    /// <summary>Saves changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}