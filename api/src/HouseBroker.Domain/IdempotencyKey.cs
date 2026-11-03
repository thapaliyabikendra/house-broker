namespace HouseBroker.Domain;

/// <summary>Idempotency key record.</summary>
public sealed class IdempotencyKey
{
    private IdempotencyKey()
    {
    }

    /// <summary>Initializes a key record.</summary>
    public static IdempotencyKey Create(string key, string scope, string requestHash, DateTime expiresAt)
        => new()
        {
            Id = Guid.CreateVersion7(),
            Key = key,
            Scope = scope,
            RequestHash = requestHash,
            ExpiresAt = expiresAt,
        };

    /// <summary>Gets the identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the key.</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>Gets the scope.</summary>
    public string Scope { get; private set; } = string.Empty;

    /// <summary>Gets the request hash.</summary>
    public string RequestHash { get; private set; } = string.Empty;

    /// <summary>Gets the stored status code.</summary>
    public int? StatusCode { get; private set; }

    /// <summary>Gets the stored response body.</summary>
    public string? ResponseBody { get; private set; }

    /// <summary>Gets whether processing completed.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Gets the creation timestamp.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Gets the updater.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>Gets the updated timestamp.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Gets the updater.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Gets the expiry timestamp.</summary>
    public DateTime ExpiresAt { get; private set; }

    /// <summary>Gets the concurrency token.</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>Marks processing as complete.</summary>
    public void Complete(int statusCode, string? responseBody)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        IsCompleted = true;
    }
}
