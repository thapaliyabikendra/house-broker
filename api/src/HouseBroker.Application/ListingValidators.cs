using FluentValidation;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Common;
using System.Text.RegularExpressions;

namespace HouseBroker.Application.Services;

/// <summary>Validator for create listing requests.</summary>
public sealed class CreateListingRequestValidator : AbstractValidator<CreateListingRequest>
{
    /// <summary>Initializes the validator.</summary>
    public CreateListingRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty().WithErrorCode(ErrorCodes.Required)
            .MaximumLength(Limits.MaxTitleLength).WithErrorCode(ErrorCodes.OutOfRange)
            .Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.HtmlNotAllowed);

        RuleFor(request => request.PropertyTypeId).NotEqual(Guid.Empty).WithErrorCode(ErrorCodes.Required);

        RuleFor(request => request.Price).GreaterThanOrEqualTo(Limits.MinPrice).WithErrorCode(ErrorCodes.OutOfRange);

        RuleFor(request => request.Province).NotEmpty().WithErrorCode(ErrorCodes.Required)
            .MaximumLength(100).WithErrorCode(ErrorCodes.OutOfRange)
            .Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.HtmlNotAllowed);
        RuleFor(request => request.City).NotEmpty().WithErrorCode(ErrorCodes.Required)
            .MaximumLength(100).WithErrorCode(ErrorCodes.OutOfRange)
            .Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.HtmlNotAllowed);
        RuleFor(request => request.Locality).NotEmpty().WithErrorCode(ErrorCodes.Required)
            .MaximumLength(100).WithErrorCode(ErrorCodes.OutOfRange)
            .Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.HtmlNotAllowed);
        RuleFor(request => request.PostalCode).Matches("^[0-9]{5}$").WithErrorCode(ErrorCodes.InvalidFormat);

        RuleFor(request => request.Bedrooms).InclusiveBetween(0, 50).WithErrorCode(ErrorCodes.OutOfRange);
        RuleFor(request => request.Bathrooms).InclusiveBetween(0, 50).WithErrorCode(ErrorCodes.OutOfRange);
        RuleFor(request => request.AreaSqFt).GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.OutOfRange);

        RuleFor(request => request.Description).Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.HtmlNotAllowed);
        RuleForEach(request => request.FeatureIds).NotEqual(Guid.Empty).WithErrorCode(ErrorCodes.Required);
    }
}

/// <summary>Validator for update listing requests.</summary>
public sealed class UpdateListingRequestValidator : AbstractValidator<UpdateListingRequest>
{
    /// <summary>Initializes the validator.</summary>
    public UpdateListingRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty().WithErrorCode(ErrorCodes.Required)
            .MaximumLength(Limits.MaxTitleLength).WithErrorCode(ErrorCodes.OutOfRange)
            .Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.HtmlNotAllowed);

        RuleFor(request => request.PropertyTypeId).NotEqual(Guid.Empty).WithErrorCode(ErrorCodes.Required);

        RuleFor(request => request.Price).GreaterThanOrEqualTo(Limits.MinPrice).WithErrorCode(ErrorCodes.OutOfRange);

        RuleFor(request => request.Province).NotEmpty().WithErrorCode(ErrorCodes.Required)
            .MaximumLength(100).WithErrorCode(ErrorCodes.OutOfRange)
            .Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.HtmlNotAllowed);
        RuleFor(request => request.City).NotEmpty().WithErrorCode(ErrorCodes.Required)
            .MaximumLength(100).WithErrorCode(ErrorCodes.OutOfRange)
            .Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.HtmlNotAllowed);
        RuleFor(request => request.Locality).NotEmpty().WithErrorCode(ErrorCodes.Required)
            .MaximumLength(100).WithErrorCode(ErrorCodes.OutOfRange)
            .Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.HtmlNotAllowed);
        RuleFor(request => request.PostalCode).Matches("^[0-9]{5}$").WithErrorCode(ErrorCodes.InvalidFormat);

        RuleFor(request => request.Bedrooms).InclusiveBetween(0, 50).WithErrorCode(ErrorCodes.OutOfRange);
        RuleFor(request => request.Bathrooms).InclusiveBetween(0, 50).WithErrorCode(ErrorCodes.OutOfRange);
        RuleFor(request => request.AreaSqFt).GreaterThanOrEqualTo(0).WithErrorCode(ErrorCodes.OutOfRange);

        RuleFor(request => request.Description).Must(ListingValidationChecks.NotContainHtml).WithErrorCode(ErrorCodes.HtmlNotAllowed);
        RuleForEach(request => request.FeatureIds).NotEqual(Guid.Empty).WithErrorCode(ErrorCodes.Required);
    }
}

internal static class ListingValidationChecks
{
    internal static bool NotContainHtml(string? value) => value is null || !Regex.IsMatch(value, "<[^>]*>");
}
