namespace HouseBroker.Domain.Shared;

/// <summary>Exception raised when a domain invariant is violated.</summary>
public sealed class DomainException : Exception
{
    /// <summary>Initializes a domain exception.</summary>
    public DomainException(string code, params object?[] arguments)
        : base(code)
    {
        Code = code;
        Arguments = arguments ?? Array.Empty<object?>();
    }

    /// <summary>Gets the stable error code.</summary>
    public string Code { get; }

    /// <summary>Gets the code arguments.</summary>
    public IReadOnlyList<object?> Arguments { get; }
}
