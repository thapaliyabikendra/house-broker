namespace HouseBroker.Application.Contracts;

/// <summary>Lookup and metadata service.</summary>
public interface ILookupService
{
    /// <summary>Gets property types.</summary>
    Task<Result<IReadOnlyList<PropertyTypeDto>>> GetPropertyTypesAsync(CancellationToken cancellationToken);

    /// <summary>Gets features.</summary>
    Task<Result<IReadOnlyList<FeatureDto>>> GetFeaturesAsync(CancellationToken cancellationToken);

    /// <summary>Gets deployment metadata.</summary>
    Task<Result<MetaDto>> GetMetaAsync(CancellationToken cancellationToken);
}
