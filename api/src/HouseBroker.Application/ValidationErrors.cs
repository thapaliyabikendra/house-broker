using FluentValidation.Results;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Common;

namespace HouseBroker.Application.Services;

/// <summary>Converts FluentValidation failures into an application error that reports every failing field.</summary>
internal static class ValidationErrors
{
    /// <summary>Builds a validation error carrying one entry per failing field, using each rule's stable error code.</summary>
    /// <param name="validation">The failed validation result.</param>
    /// <returns>A validation error whose <see cref="ApplicationError.FieldErrors"/> lists all failures.</returns>
    internal static ApplicationError FromValidation(ValidationResult validation)
    {
        var fieldErrors = validation.Errors
            .Select(failure => new FieldError(
                string.IsNullOrEmpty(failure.PropertyName) ? "request" : failure.PropertyName,
                failure.ErrorCode ?? ErrorCodes.ValidationFailed))
            .ToList();

        return new ApplicationError(ErrorCodes.ValidationFailed, ErrorKind.Validation, fieldErrors.FirstOrDefault()?.Field)
        {
            FieldErrors = fieldErrors,
        };
    }
}
