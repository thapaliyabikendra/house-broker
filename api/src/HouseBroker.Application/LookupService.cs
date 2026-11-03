using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Shared;

namespace HouseBroker.Application.Services;

/// <summary>Lookup and metadata service implementation.</summary>
public sealed class LookupService(IListingQueries queries) : ILookupService
{
    private readonly IListingQueries _queries = queries;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PropertyTypeDto>>> GetPropertyTypesAsync(CancellationToken cancellationToken)
    {
        var items = await _queries.GetPropertyTypesAsync(cancellationToken);
        return Result<IReadOnlyList<PropertyTypeDto>>.Success(items.Select(item => new PropertyTypeDto
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            SortOrder = item.SortOrder,
        }).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<FeatureDto>>> GetFeaturesAsync(CancellationToken cancellationToken)
    {
        var items = await _queries.GetFeaturesAsync(cancellationToken);
        return Result<IReadOnlyList<FeatureDto>>.Success(items.Select(item => new FeatureDto
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            SortOrder = item.SortOrder,
        }).ToList());
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

        return Task.FromResult(Result<MetaDto>.Success(meta));
    }
}
