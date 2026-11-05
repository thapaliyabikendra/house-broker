namespace HouseBroker.Application.Contracts;

/// <summary>
/// An existing record that must be fixed before the request can succeed, with the raw values needed to describe it to the caller.
/// For commission bands, the arguments are <c>[maxPrice-or-null, maxInclusive]</c>.
/// </summary>
public sealed record RelatedRecord
{
    /// <summary>Initializes a related record.</summary>
    /// <param name="id">The record id (also listed in <see cref="ApplicationError.RelatedIds"/>).</param>
    /// <param name="args">The raw values that describe the record. Empty when they are not available.</param>
    public RelatedRecord(Guid id, params object?[] args)
    {
        Id = id;
        Args = args;
    }

    /// <summary>Gets the record id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the raw values that describe the record.</summary>
    public IReadOnlyList<object?> Args { get; init; }
}
