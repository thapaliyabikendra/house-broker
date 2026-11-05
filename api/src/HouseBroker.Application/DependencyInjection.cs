using HouseBroker.Application.Contracts.Listings;
using HouseBroker.Application.Contracts.Lookups;
using HouseBroker.Application.Contracts.CommissionRates;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace HouseBroker.Application;

/// <summary>Application dependency injection.</summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>Adds application services and validators.</summary>
    public static IServiceCollection AddHouseBrokerApplication(this IServiceCollection services)
    {
        services.AddScoped<IListingService, HouseBroker.Application.Listings.ListingService>();
        services.AddScoped<ICommissionRateAdminService, HouseBroker.Application.CommissionRates.CommissionRateAdminService>();
        services.AddScoped<ILookupService, HouseBroker.Application.Lookups.LookupService>();
        services.AddScoped<IValidator<CreateListingRequest>, HouseBroker.Application.Listings.CreateListingRequestValidator>();
        services.AddScoped<IValidator<UpdateListingRequest>, HouseBroker.Application.Listings.UpdateListingRequestValidator>();
        services.AddScoped<IValidator<CommissionRateRequest>, HouseBroker.Application.CommissionRates.CommissionRateRequestValidator>();
        return services;
    }
}
