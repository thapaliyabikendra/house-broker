using HouseBroker.Application.Contracts;
using HouseBroker.Domain;
using HouseBroker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace HouseBroker.Infrastructure.Idempotency;

/// <summary>Idempotency service implementation.</summary>
public sealed class IdempotencyService(AppDbContext dbContext, TimeProvider clock) : IIdempotencyService
{
    private readonly AppDbContext _dbContext = dbContext;
    private readonly TimeProvider _clock = clock;

    /// <inheritdoc />
    public async Task<IdempotencyResult> BeginAsync(string key, string scope, string requestHash, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        var existing = await _dbContext.IdempotencyKeys.FirstOrDefaultAsync(item => item.Key == key && item.Scope == scope, cancellationToken);
        if (existing is not null)
        {
            if (existing.ExpiresAt <= now)
            {
                _dbContext.IdempotencyKeys.Remove(existing);
                await _dbContext.SaveChangesAsync(cancellationToken);
                existing = null;
            }
            else
            {
                if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                {
                    return new IdempotencyResult(IdempotencyStatus.Conflict, null, null);
                }

                if (!existing.IsCompleted)
                {
                    return new IdempotencyResult(IdempotencyStatus.InProgress, null, null);
                }

                return new IdempotencyResult(IdempotencyStatus.Replay, existing.StatusCode, existing.ResponseBody);
            }
        }

        var record = IdempotencyKey.Create(key, scope, requestHash, now.AddHours(24));
        _dbContext.IdempotencyKeys.Add(record);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var concurrent = await _dbContext.IdempotencyKeys.FirstOrDefaultAsync(item => item.Key == key && item.Scope == scope, cancellationToken);
            if (concurrent is null)
            {
                throw;
            }

            if (!string.Equals(concurrent.RequestHash, requestHash, StringComparison.Ordinal))
            {
                return new IdempotencyResult(IdempotencyStatus.Conflict, null, null);
            }

            if (!concurrent.IsCompleted)
            {
                return new IdempotencyResult(IdempotencyStatus.InProgress, null, null);
            }

            return new IdempotencyResult(IdempotencyStatus.Replay, concurrent.StatusCode, concurrent.ResponseBody);
        }

        return new IdempotencyResult(IdempotencyStatus.New, null, null);
    }

    /// <inheritdoc />
    public async Task CompleteAsync(string key, string scope, int statusCode, string? responseBody, CancellationToken cancellationToken)
    {
        var record = await _dbContext.IdempotencyKeys.FirstOrDefaultAsync(item => item.Key == key && item.Scope == scope, cancellationToken);
        if (record is null)
        {
            return;
        }

        record.Complete(statusCode, responseBody);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(string key, string scope, CancellationToken cancellationToken)
    {
        var record = await _dbContext.IdempotencyKeys.FirstOrDefaultAsync(item => item.Key == key && item.Scope == scope, cancellationToken);
        if (record is null || record.IsCompleted)
        {
            return;
        }

        _dbContext.IdempotencyKeys.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Computes a stable hash for a request body.</summary>
    public static string ComputeHash(string body)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(body ?? string.Empty));
        return Convert.ToHexString(bytes);
    }
}
