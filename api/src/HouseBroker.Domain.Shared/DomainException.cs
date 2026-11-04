namespace HouseBroker.Domain.Common;

/// <summary>Exception raised when a domain invariant is violated.</summary>
public sealed class DomainException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DomainException"/> class.</summary>
    public DomainException() : base()
    {
        Code = string.Empty;
        Arguments = Array.Empty<object?>();
    }

    /// <summary>Initializes a new instance of the <see cref="DomainException"/> class with a specified error message.</summary>
    public DomainException(string message) : base(message)
    {
        Code = string.Empty;
        Arguments = Array.Empty<object?>();
    }

    /// <summary>Initializes a new instance of the <see cref="DomainException"/> class with a specified error message and a reference to the inner exception that is evident causing this exception.</summary>
    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
        Code = string.Empty;
        Arguments = Array.Empty<object?>();
    }

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
