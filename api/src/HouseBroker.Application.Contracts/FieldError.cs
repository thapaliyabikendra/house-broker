namespace HouseBroker.Application.Contracts;

/// <summary>One field-level failure: the input field and its stable error code.</summary>
/// <param name="Field">The input property name as the application sees it (PascalCase, may include an index such as <c>FeatureIds[0]</c>).</param>
/// <param name="Code">The stable error code (for example <c>validation.failed</c>). Never user-facing text.</param>
public sealed record FieldError(string Field, string Code);
