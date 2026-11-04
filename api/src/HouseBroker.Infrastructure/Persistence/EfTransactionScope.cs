using HouseBroker.Application.Contracts;
using Microsoft.EntityFrameworkCore.Storage;
namespace HouseBroker.Infrastructure.Persistence;

/// <summary>EF Core transaction adapter.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Instantiated when a serializable unit of work transaction is created.")]
internal sealed class EfTransactionScope(IDbContextTransaction transaction) : ITransactionScope
{
    private bool _committed;

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await transaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await transaction.RollbackAsync(cancellationToken);
        _committed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_committed)
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch
            {
                // Ignore rollback failures during disposal.
            }
        }

        await transaction.DisposeAsync();
    }
}
