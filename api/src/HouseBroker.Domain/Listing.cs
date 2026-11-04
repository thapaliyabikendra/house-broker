namespace HouseBroker.Domain;

/// <summary>Property listing aggregate root.</summary>
public sealed class Listing
{
    private Listing()
    {
    }

    /// <summary>
    /// Initializes a new listing.
    /// </summary>
    public static Listing Create(
        Guid brokerId,
        string title,
        string description,
        Guid propertyTypeId,
        decimal price,
        string province,
        string city,
        string locality,
        string postalCode,
        int bedrooms,
        int bathrooms,
        decimal areaSqFt)
    {
        if (brokerId == Guid.Empty)
        {
            throw new HouseBroker.Domain.Common.DomainException(HouseBroker.Domain.Common.ErrorCodes.Required, nameof(brokerId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new HouseBroker.Domain.Common.DomainException(HouseBroker.Domain.Common.ErrorCodes.Required, nameof(title));
        }

        return new Listing
        {
            Id = Guid.CreateVersion7(),
            BrokerId = brokerId,
            Title = title.Trim(),
            Description = description?.Trim() ?? string.Empty,
            PropertyTypeId = propertyTypeId,
            Price = price,
            Province = province?.Trim() ?? string.Empty,
            City = city?.Trim() ?? string.Empty,
            Locality = locality?.Trim() ?? string.Empty,
            PostalCode = postalCode?.Trim() ?? string.Empty,
            Bedrooms = bedrooms,
            Bathrooms = bathrooms,
            AreaSqFt = areaSqFt,
            Status = HouseBroker.Domain.Common.ListingStatus.Draft,
            CommissionRateId = Guid.Empty,
        };
    }

    /// <summary>Gets the listing identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Gets the owning broker identifier.</summary>
    public Guid BrokerId { get; private set; }

    /// <summary>Gets the title.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets the description.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Gets the property type identifier.</summary>
    public Guid PropertyTypeId { get; private set; }

    /// <summary>Gets the price.</summary>
    public decimal Price { get; private set; }

    /// <summary>Gets the province.</summary>
    public string Province { get; private set; } = string.Empty;

    /// <summary>Gets the city.</summary>
    public string City { get; private set; } = string.Empty;

    /// <summary>Gets the locality.</summary>
    public string Locality { get; private set; } = string.Empty;

    /// <summary>Gets the postal code.</summary>
    public string PostalCode { get; private set; } = string.Empty;

    /// <summary>Gets the number of bedrooms.</summary>
    public int Bedrooms { get; private set; }

    /// <summary>Gets the number of bathrooms.</summary>
    public int Bathrooms { get; private set; }

    /// <summary>Gets the area in square feet.</summary>
    public decimal AreaSqFt { get; private set; }

    /// <summary>Gets the status.</summary>
    public HouseBroker.Domain.Common.ListingStatus Status { get; private set; }

    /// <summary>Gets the commission amount.</summary>
    public decimal CommissionAmount { get; private set; }

    /// <summary>Gets the commission rate percent.</summary>
    public decimal CommissionRatePercent { get; private set; }

    /// <summary>Gets the commission rate identifier.</summary>
    public Guid CommissionRateId { get; private set; }

    private readonly List<ListingImage> _images = new();

    /// <summary>Gets the images.</summary>
    public IReadOnlyCollection<ListingImage> Images => _images;

    private readonly List<ListingFeature> _features = new();

    /// <summary>Gets the features.</summary>
    public IReadOnlyCollection<ListingFeature> Features => _features;

    /// <summary>Gets the created timestamp.</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Gets the creator.</summary>
    public Guid? CreatedBy { get; private set; }

    /// <summary>Gets the updated timestamp.</summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Gets the updater.</summary>
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Gets whether the listing is deleted.</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Gets the deletion timestamp.</summary>
    public DateTime? DeletedAt { get; private set; }

    /// <summary>Gets the deleter.</summary>
    public Guid? DeletedBy { get; private set; }

    /// <summary>Gets the concurrency token.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "EF Core requires a byte array for rowversion concurrency tokens.")]
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>Adds a feature.</summary>
    public void AddFeature(Guid featureId)
    {
        if (!_features.Any(feature => feature.FeatureId == featureId))
        {
            _features.Add(ListingFeature.Create(Id, featureId));
        }
    }

    /// <summary>Adds an image, maintaining the primary-image and ordering invariants.</summary>
    public void AddImage(ListingImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.IsPrimary)
        {
            foreach (var existing in _images)
            {
                existing.ClearPrimary();
            }
        }
        else if (_images.Count == 0)
        {
            image.MarkPrimary();
        }

        image.SetSortOrder(_images.Count == 0 ? 0 : _images.Max(item => item.SortOrder) + 1);
        _images.Add(image);
    }

    /// <summary>Removes an image and promotes the next image when the primary is removed.</summary>
    public void RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(item => item.Id == imageId);
        if (image is null)
        {
            return;
        }

        _images.Remove(image);

        if (image.IsPrimary)
        {
            _images
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .FirstOrDefault()
                ?.MarkPrimary();
        }
    }

    /// <summary>Replaces features, updating only the rows that actually changed.</summary>
    public void ReplaceFeatures(IEnumerable<Guid>? featureIds)
    {
        var desired = (featureIds ?? Enumerable.Empty<Guid>())
            .Where(featureId => featureId != Guid.Empty)
            .Distinct()
            .ToList();
        var desiredSet = desired.ToHashSet();

        foreach (var existing in _features.Where(feature => !desiredSet.Contains(feature.FeatureId)).ToList())
        {
            _features.Remove(existing);
        }

        foreach (var featureId in desired.Where(featureId => _features.All(feature => feature.FeatureId != featureId)))
        {
            _features.Add(ListingFeature.Create(Id, featureId));
        }
    }

    /// <summary>Updates basic details.</summary>
    public void UpdateDetails(
        string title,
        string description,
        Guid propertyTypeId,
        decimal price,
        string province,
        string city,
        string locality,
        string postalCode,
        int bedrooms,
        int bathrooms,
        decimal areaSqFt)
    {
        ArgumentNullException.ThrowIfNull(title);
        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        PropertyTypeId = propertyTypeId;
        if (Price != price)
        {
            ChangePrice(price);
        }

        Province = province?.Trim() ?? string.Empty;
        City = city?.Trim() ?? string.Empty;
        Locality = locality?.Trim() ?? string.Empty;
        PostalCode = postalCode?.Trim() ?? string.Empty;
        Bedrooms = bedrooms;
        Bathrooms = bathrooms;
        AreaSqFt = areaSqFt;
    }

    /// <summary>Publishes the listing.</summary>
    public void Publish()
    {
        if (Status != HouseBroker.Domain.Common.ListingStatus.Draft)
        {
            throw new HouseBroker.Domain.Common.DomainException(HouseBroker.Domain.Common.ErrorCodes.Conflict, Status);
        }

        Status = HouseBroker.Domain.Common.ListingStatus.Active;
    }

    /// <summary>Marks the listing as sold.</summary>
    public void MarkSold()
    {
        if (Status != HouseBroker.Domain.Common.ListingStatus.Active)
        {
            throw new HouseBroker.Domain.Common.DomainException(HouseBroker.Domain.Common.ErrorCodes.Conflict, Status);
        }

        Status = HouseBroker.Domain.Common.ListingStatus.Sold;
    }

    /// <summary>Archives the listing.</summary>
    public void Archive()
    {
        if (Status == HouseBroker.Domain.Common.ListingStatus.Archived)
        {
            return;
        }

        Status = HouseBroker.Domain.Common.ListingStatus.Archived;
    }

    /// <summary>Soft deletes the listing.</summary>
    public void SoftDelete()
    {
        IsDeleted = true;
    }

    /// <summary>Marks the aggregate as changed so its row version advances.</summary>
    public void Touch(DateTime utcNow, Guid? actorId)
    {
        UpdatedAt = utcNow;
        UpdatedBy = actorId;
    }

    /// <summary>Applies a commission snapshot.</summary>
    public void ApplyCommission(decimal amount, decimal ratePercent, Guid rateId)
    {
        CommissionAmount = amount;
        CommissionRatePercent = ratePercent;
        CommissionRateId = rateId;
    }

    /// <summary>Changes the price and recalculates commission via caller.</summary>
    public void ChangePrice(decimal newPrice)
    {
        if (newPrice < 0)
        {
            throw new HouseBroker.Domain.Common.DomainException(HouseBroker.Domain.Common.ErrorCodes.OutOfRange, nameof(newPrice));
        }

        Price = newPrice;
    }
}
