namespace HouseBroker.Application.Contracts;

/// <summary>
/// One field-level failure: the input field, its stable error code and the values that fill the message placeholders.
/// Arguments are raw domain values (decimals, integers, strings), never user-facing text; the API boundary formats and localizes them.
/// </summary>
public sealed record FieldError
{
    /// <summary>Initializes a field error.</summary>
    /// <param name="field">The input property name as the application sees it (PascalCase, may include an index such as <c>FeatureIds[0]</c>).</param>
    /// <param name="code">The stable error code (for example <c>validation.failed</c>). Never user-facing text.</param>
    /// <param name="args">Message arguments in placeholder order ({0}, {1}, ...). Empty when the message needs none.</param>
    public FieldError(string field, string code, params object?[] args)
    {
        Field = field;
        Code = code;
        Args = args;
    }

    /// <summary>Gets the input property name.</summary>
    public string Field { get; init; }

    /// <summary>Gets the stable error code.</summary>
    public string Code { get; init; }

    /// <summary>Gets the message arguments in placeholder order.</summary>
    public IReadOnlyList<object?> Args { get; init; }
}
