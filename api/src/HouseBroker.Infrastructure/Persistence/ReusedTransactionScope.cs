using HouseBroker.Application.Contracts;
using Microsoft.EntityFrameworkCore.Storage;

namespace HouseBroker.Infrastructure.Persistence;

/// <summary>Transaction scope adapter for an existing ambient database transaction.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Returned when a transaction is already active.")]
internal sealed class ReusedTransactionScope : ITransactionScope
{
    public ReusedTransactionScope(IDbContextTransaction transaction)
    {
        _ = transaction;
    }

    /// <inheritdoc />
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}