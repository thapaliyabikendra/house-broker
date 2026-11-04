namespace HouseBroker.Application.Contracts;

/// <summary>Idempotency status.</summary>
public enum IdempotencyStatus
{
    /// <summary>A new key was created.</summary>
    New,

    /// <summary>A previous completed result should be replayed.</summary>
    Replay,

    /// <summary>The key exists with a different request payload.</summary>
    Conflict,

    /// <summary>The key exists but processing has not completed.</summary>
    InProgress,
}

/// <summary>Idempotency begin result.</summary>
public sealed record IdempotencyResult(IdempotencyStatus Status, int? StatusCode, string? ResponseBody);

/// <summary>Idempotency service.</summary>
public interface IIdempotencyService
{
    /// <summary>Computes the stable hash that identifies a request payload.</summary>
    string ComputeRequestHash(string body);

    /// <summary>Begins idempotent processing.</summary>
    Task<IdempotencyResult> BeginAsync(string key, string scope, string requestHash, CancellationToken cancellationToken);

    /// <summary>Completes idempotent processing.</summary>
    Task CompleteAsync(string key, string scope, int statusCode, string? responseBody, CancellationToken cancellationToken);

    /// <summary>Releases a failed idempotent processing attempt.</summary>
    Task ReleaseAsync(string key, string scope, CancellationToken cancellationToken);
}
