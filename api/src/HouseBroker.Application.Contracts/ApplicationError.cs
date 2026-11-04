namespace HouseBroker.Application.Contracts;

/// <summary>Expected failure details.</summary>
public sealed record ApplicationError
{
    /// <summary>Initializes an error.</summary>
    public ApplicationError(string code, ErrorKind kind, string? field = null, params object?[] arguments)
    {
        Code = code;
        Kind = kind;
        Field = field;
        Arguments = arguments ?? Array.Empty<object?>();
    }

    /// <summary>Gets the stable error code.</summary>
    public string Code { get; init; }

    /// <summary>Gets the failure kind.</summary>
    public ErrorKind Kind { get; init; }

    /// <summary>Gets the optional field name.</summary>
    public string? Field { get; init; }

    /// <summary>Gets the code arguments.</summary>
    public IReadOnlyList<object?> Arguments { get; init; }

    /// <summary>Gets every failing field. Empty when the error is not tied to input fields.</summary>
    public IReadOnlyList<FieldError> FieldErrors { get; init; } = Array.Empty<FieldError>();

    /// <summary>Gets the ids of existing records that cause the failure and that the caller must fix (for example, commission tiers that already break the tier rules).</summary>
    public IReadOnlyList<Guid> RelatedIds { get; init; } = Array.Empty<Guid>();
}
