namespace HouseBroker.Application.Contracts;

/// <summary>Coordinates persistence.</summary>
public interface IUnitOfWork
{
    /// <summary>Saves changes.</summary>
    Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Begins a database transaction.</summary>
    Task<ITransactionScope> BeginTransactionAsync(TransactionIsolationLevel isolationLevel = TransactionIsolationLevel.Serializable, CancellationToken cancellationToken = default);
}
