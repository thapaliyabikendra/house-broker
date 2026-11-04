namespace HouseBroker.Application.Contracts;

/// <summary>Represents a scope for a database transaction.</summary>
public interface ITransactionScope : IAsyncDisposable
{
    /// <summary>Commits the transaction.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back the transaction.</summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
