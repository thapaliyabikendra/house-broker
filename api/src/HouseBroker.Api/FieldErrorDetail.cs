namespace HouseBroker.Api;

/// <summary>One field-level error on the wire: the stable code and its localized message.</summary>
/// <param name="Code">The stable error code (for example <c>validation.failed</c>).</param>
/// <param name="Message">The message localized for the request culture.</param>
public sealed record FieldErrorDetail(string Code, string Message);
