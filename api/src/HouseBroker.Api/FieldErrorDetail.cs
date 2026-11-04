namespace HouseBroker.Api;

/// <summary>One field-level error on the wire: the stable code, its localized message and the formatted arguments it used.</summary>
/// <param name="Code">The stable error code (for example <c>validation.failed</c>).</param>
/// <param name="Message">The message localized for the request culture.</param>
/// <param name="Args">The message arguments formatted for the request culture (for example <c>50,00,000</c>). Empty when the message has none.</param>
public sealed record FieldErrorDetail(string Code, string Message, IReadOnlyList<string> Args);
