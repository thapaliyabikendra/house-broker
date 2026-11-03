using FluentValidation;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Shared;
using System.Text.RegularExpressions;

namespace HouseBroker.Application.Services;

/// <summary>Validator for create listing requests.</summary>
public sealed class CreateListingRequestValidator : AbstractValidator<CreateListingRequest>
{
    /// <summary>Initializes the validator.</summary>
    public CreateListingRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(Limits.MaxTitleLength)
            .Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.ValidationFailed);

        RuleFor(request => request.PropertyTypeId).NotEqual(Guid.Empty).WithErrorCode(ErrorCodes.ValidationFailed);

        RuleFor(request => request.Price).GreaterThanOrEqualTo(Limits.MinPrice).WithErrorCode(ErrorCodes.ValidationFailed);

        RuleFor(request => request.Province).NotEmpty().MaximumLength(100).Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleFor(request => request.City).NotEmpty().MaximumLength(100).Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleFor(request => request.Locality).NotEmpty().MaximumLength(100).Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleFor(request => request.PostalCode).Matches("^[0-9]{5}$").WithErrorCode(ErrorCodes.ValidationFailed);

        RuleFor(request => request.Bedrooms).InclusiveBetween(0, 50).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleFor(request => request.Bathrooms).InclusiveBetween(0, 50).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleFor(request => request.AreaSqFt).GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.ValidationFailed);

        RuleFor(request => request.Description).Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleForEach(request => request.FeatureIds).NotEqual(Guid.Empty).WithErrorCode(ErrorCodes.ValidationFailed);
    }
}

/// <summary>Validator for update listing requests.</summary>
public sealed class UpdateListingRequestValidator : AbstractValidator<UpdateListingRequest>
{
    /// <summary>Initializes the validator.</summary>
    public UpdateListingRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(Limits.MaxTitleLength)
            .Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.ValidationFailed);

        RuleFor(request => request.PropertyTypeId).NotEqual(Guid.Empty).WithErrorCode(ErrorCodes.ValidationFailed);

        RuleFor(request => request.Price).GreaterThanOrEqualTo(Limits.MinPrice).WithErrorCode(ErrorCodes.ValidationFailed);

        RuleFor(request => request.Province).NotEmpty().MaximumLength(100).Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleFor(request => request.City).NotEmpty().MaximumLength(100).Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleFor(request => request.Locality).NotEmpty().MaximumLength(100).Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleFor(request => request.PostalCode).Matches("^[0-9]{5}$").WithErrorCode(ErrorCodes.ValidationFailed);

        RuleFor(request => request.Bedrooms).InclusiveBetween(0, 50).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleFor(request => request.Bathrooms).InclusiveBetween(0, 50).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleFor(request => request.AreaSqFt).GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.ValidationFailed);

        RuleFor(request => request.Description).Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.ValidationFailed);
        RuleForEach(request => request.FeatureIds).NotEqual(Guid.Empty).WithErrorCode(ErrorCodes.ValidationFailed);
    }
}

internal static class ListingValidationChecks
{
    internal static bool NotContainHtml(string? value) => value is null || !Regex.IsMatch(value, "<[^>]*>");
}
