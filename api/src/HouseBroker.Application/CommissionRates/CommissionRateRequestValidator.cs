using FluentValidation;
using HouseBroker.Application.Contracts.CommissionRates;
using HouseBroker.Domain.Common;

namespace HouseBroker.Application.CommissionRates;

/// <summary>Validates a commission rate band write request before the domain guard runs.</summary>
public sealed class CommissionRateRequestValidator : AbstractValidator<CommissionRateRequest>
{
    /// <summary>Initializes the validator.</summary>
    public CommissionRateRequestValidator()
    {
        RuleFor(request => request.MaxPrice)
            .GreaterThan(0m)
            .When(request => request.MaxPrice.HasValue)
            .WithErrorCode(ErrorCodes.OutOfRange);

        RuleFor(request => request.RatePercent)
            .GreaterThan(0m)
            .LessThanOrEqualTo(100m)
            .WithErrorCode(ErrorCodes.OutOfRange);
    }
}
