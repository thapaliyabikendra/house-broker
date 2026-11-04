using HouseBroker.Application.Abstractions;
using HouseBroker.Application.Contracts.Lookups;
using HouseBroker.Application.Contracts;
using HouseBroker.Application.Mapping;
using HouseBroker.Domain.Common;

namespace HouseBroker.Application.Lookups;

/// <summary>Lookup and metadata service implementation.</summary>
public sealed class LookupService(IListingQueries queries) : ILookupService
{
    private readonly IListingQueries _queries = queries;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PropertyTypeDto>>> GetPropertyTypesAsync(CancellationToken cancellationToken)
    {
        var items = await _queries.GetPropertyTypesAsync(cancellationToken);
        return Result.Success<IReadOnlyList<PropertyTypeDto>>(items.Select(ListingMapper.Instance.ToPropertyType).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<FeatureDto>>> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        var items = await _queries.GetFeaturesAsync(cancellationToken);
        return Result.Success<IReadOnlyList<FeatureDto>>(items.Select(ListingMapper.Instance.ToFeatureLookup).ToList());
    }

    /// <inheritdoc />
    public Task<Result<MetaDto>> GetMetaAsync(CancellationToken cancellationToken)
    {
        var meta = new MetaDto
        {
            DefaultCulture = SupportedCultures.Default,
            SupportedCultures = SupportedCultures.All,
            CurrencyCode = SupportedCultures.DefaultCurrency,
            MaxPageSize = Limits.MaxPageSize,
            MaxSearchTextLength = Limits.MaxSearchTextLength,
        };

        return Task.FromResult(Result.Success<MetaDto>(meta));
    }
}
