using HouseBroker.Application.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HouseBroker.Infrastructure.Persistence;

/// <summary>Sets audit columns centrally.</summary>
public sealed class AuditSaveChangesInterceptor(TimeProvider timeProvider, ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var actor = currentUserService.UserId;

        foreach (var entry in eventData.Context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                SetIfPresent(entry, "CreatedAt", now);
                SetIfPresent(entry, "CreatedBy", actor);
                SetIfPresent(entry, "UpdatedAt", now);
                SetIfPresent(entry, "UpdatedBy", actor);
            }
            else if (entry.State == EntityState.Modified)
            {
                SetIfPresent(entry, "UpdatedAt", now);
                SetIfPresent(entry, "UpdatedBy", actor);
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void SetIfPresent(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, string propertyName, object? value)
    {
        if (entry.Metadata.FindProperty(propertyName) is null)
        {
            return;
        }

        entry.Property(propertyName).CurrentValue = value;
    }
}