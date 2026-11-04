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
}
