using HouseBroker.Application.Contracts;
using HouseBroker.Domain;
using HouseBroker.Domain.Shared;

namespace HouseBroker.Application.Services;

/// <summary>Listing service implementation.</summary>
public sealed class ListingService(
    IListingRepository repository,
    IListingQueries queries,
    ICommissionRateProvider commissionRates,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    FluentValidation.IValidator<HouseBroker.Application.Contracts.CreateListingRequest> createValidator,
    FluentValidation.IValidator<HouseBroker.Application.Contracts.UpdateListingRequest> updateValidator) : IListingService
{
    private readonly IListingRepository _repository = repository;
    private readonly IListingQueries _queries = queries;
    private readonly ICommissionRateProvider _commissionRates = commissionRates;
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _clock = clock;
    private readonly FluentValidation.IValidator<HouseBroker.Application.Contracts.CreateListingRequest> _createValidator = createValidator;
    private readonly FluentValidation.IValidator<HouseBroker.Application.Contracts.UpdateListingRequest> _updateValidator = updateValidator;

    /// <inheritdoc />
    public async Task<Result<OwnerListingDto>> CreateAsync(CreateListingRequest request, CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var createValidation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!createValidation.IsValid)
        {
            var failure = createValidation.Errors[0];
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.ValidationFailed, ErrorKind.Validation, failure.PropertyName));
        }

        var listing = Listing.Create(
            brokerId.Value,
            request.Title,
            request.Description,
            request.PropertyTypeId,
            request.Price,
            request.Province,
            request.City,
            request.Locality,
            request.PostalCode,
            request.Bedrooms,
            request.Bathrooms,
            request.AreaSqFt);

        listing.ReplaceFeatures(request.FeatureIds);

        var tiers = await _commissionRates.GetActiveAsync(_clock.GetUtcNow().UtcDateTime, cancellationToken);
        var commission = CommissionCalculator.Calculate(request.Price, tiers, _clock.GetUtcNow().UtcDateTime);
        listing.ApplyCommission(commission.Amount, commission.RatePercent, commission.RateId);

        await _repository.AddAsync(listing, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<OwnerListingDto>.Success(MapOwner(listing));
    }

    /// <inheritdoc />
    public async Task<Result<OwnerListingDto>> UpdateAsync(Guid id, UpdateListingRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var listing = await _repository.GetByIdAsync(id, cancellationToken);
        if (listing is null)
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (listing.BrokerId != brokerId.Value)
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        var updateValidation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!updateValidation.IsValid)
        {
            var failure = updateValidation.Errors[0];
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.ValidationFailed, ErrorKind.Validation, failure.PropertyName));
        }

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.PreconditionRequired, ErrorKind.PreconditionRequired));
        }

        if (!MatchesRowVersion(listing.RowVersion, ifMatch))
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.PreconditionFailed, ErrorKind.PreconditionFailed));
        }

        var oldPrice = listing.Price;
        var oldCommissionAmount = listing.CommissionAmount;
        var oldCommissionRatePercent = listing.CommissionRatePercent;
        var oldCommissionRateId = listing.CommissionRateId;

        listing.UpdateDetails(
            request.Title,
            request.Description,
            request.PropertyTypeId,
            request.Price,
            request.Province,
            request.City,
            request.Locality,
            request.PostalCode,
            request.Bedrooms,
            request.Bathrooms,
            request.AreaSqFt);

        listing.ReplaceFeatures(request.FeatureIds);

        var tiers = await _commissionRates.GetActiveAsync(_clock.GetUtcNow().UtcDateTime, cancellationToken);
        var commission = CommissionCalculator.Calculate(request.Price, tiers, _clock.GetUtcNow().UtcDateTime);
        listing.ApplyCommission(commission.Amount, commission.RatePercent, commission.RateId);

        if (oldPrice != request.Price ||
            oldCommissionAmount != commission.Amount ||
            oldCommissionRatePercent != commission.RatePercent ||
            oldCommissionRateId != commission.RateId)
        {
            await _repository.AddPriceHistoryAsync(ListingPriceHistory.Create(
                listing.Id,
                oldPrice,
                request.Price,
                oldCommissionAmount,
                commission.Amount,
                oldCommissionRatePercent,
                commission.RatePercent,
                oldCommissionRateId == Guid.Empty ? null : oldCommissionRateId,
                commission.RateId,
                brokerId.Value,
                _clock.GetUtcNow().UtcDateTime), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<OwnerListingDto>.Success(MapOwner(listing));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            return Result.Failure(new Error(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var listing = await _repository.GetByIdAsync(id, cancellationToken);
        if (listing is null)
        {
            return Result.Failure(new Error(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (listing.BrokerId != brokerId.Value)
        {
            return Result.Failure(new Error(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        listing.SoftDelete();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<ListingDetailDto>> GetDetailAsync(Guid id, bool includeContact, CancellationToken cancellationToken)
    {
        var listing = await _queries.GetPublicAsync(id, cancellationToken);
        if (listing is null || listing.Status != ListingStatus.Active)
        {
            return Result<ListingDetailDto>.Failure(new Error(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        var dto = MapDetail(listing);
        if (includeContact)
        {
            var profile = await _queries.GetBrokerProfileAsync(listing.BrokerId, cancellationToken);
            if (profile is not null)
            {
                dto.BrokerContact = new BrokerContactDto
                {
                    FullName = profile.FullName,
                    Email = profile.Email,
                    PhoneNumber = profile.PhoneNumber,
                };
            }
        }

        return Result<ListingDetailDto>.Success(dto);
    }

    /// <inheritdoc />
    public async Task<Result<OwnerListingDto>> GetOwnerDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var listing = await _repository.GetByIdAsync(id, cancellationToken);
        if (listing is null)
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (listing.BrokerId != brokerId.Value)
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        return Result<OwnerListingDto>.Success(MapOwner(listing));
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<ListingSummaryDto>>> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken)
    {
        if (query is null)
        {
            return Result<PagedResult<ListingSummaryDto>>.Failure(new Error(ErrorCodes.Required, ErrorKind.Validation, nameof(query)));
        }

        if (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice.Value > query.MaxPrice.Value)
        {
            return Result<PagedResult<ListingSummaryDto>>.Failure(new Error(ErrorCodes.OutOfRange, ErrorKind.Validation, nameof(query.MinPrice)));
        }

        if (!string.IsNullOrWhiteSpace(query.Location) && query.Location.Trim().Length > HouseBroker.Domain.Shared.Limits.MaxSearchTextLength)
        {
            return Result<PagedResult<ListingSummaryDto>>.Failure(new Error(ErrorCodes.OutOfRange, ErrorKind.Validation, nameof(query.Location)));
        }

        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, HouseBroker.Domain.Shared.Limits.MaxPageSize);
        if (query.PageSize == 0)
        {
            query.PageSize = HouseBroker.Domain.Shared.Limits.DefaultPageSize;
        }

        var result = await _queries.SearchAsync(query, cancellationToken);
        return Result<PagedResult<ListingSummaryDto>>.Success(result);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<OwnerListingDto>>> GetMineAsync(CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            return Result<IReadOnlyList<OwnerListingDto>>.Failure(new Error(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var listings = await _queries.GetMineAsync(brokerId.Value, cancellationToken);
        return Result<IReadOnlyList<OwnerListingDto>>.Success(listings.Select(MapOwner).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<OwnerListingDto>> PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var listing = await _repository.GetByIdAsync(id, cancellationToken);
        if (listing is null)
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (listing.BrokerId != brokerId.Value)
        {
            return Result<OwnerListingDto>.Failure(new Error(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        var fromStatus = listing.Status;
        listing.Publish();
        await _repository.AddStatusHistoryAsync(ListingStatusHistory.Create(listing.Id, fromStatus, listing.Status, brokerId.Value, _clock.GetUtcNow().UtcDateTime, null), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<OwnerListingDto>.Success(MapOwner(listing));
    }

    private static ListingDetailDto MapDetail(Listing listing)
        => new()
        {
            Id = listing.Id,
            Title = listing.Title,
            Description = listing.Description,
            PropertyTypeId = listing.PropertyTypeId,
            Price = listing.Price,
            Province = listing.Province,
            City = listing.City,
            Locality = listing.Locality,
            PostalCode = listing.PostalCode,
            Bedrooms = listing.Bedrooms,
            Bathrooms = listing.Bathrooms,
            AreaSqFt = listing.AreaSqFt,
            Status = listing.Status.ToString(),
            Images = listing.Images.Select(MapImage).ToList(),
            Features = listing.Features.Select(feature => new ListingFeatureDto { Id = feature.FeatureId }).ToList(),
            CreatedAt = listing.CreatedAt,
            Version = Convert.ToBase64String(listing.RowVersion),
        };

    private static OwnerListingDto MapOwner(Listing listing)
        => new()
        {
            Id = listing.Id,
            Title = listing.Title,
            Description = listing.Description,
            PropertyTypeId = listing.PropertyTypeId,
            Price = listing.Price,
            Province = listing.Province,
            City = listing.City,
            Locality = listing.Locality,
            PostalCode = listing.PostalCode,
            Bedrooms = listing.Bedrooms,
            Bathrooms = listing.Bathrooms,
            AreaSqFt = listing.AreaSqFt,
            Status = listing.Status.ToString(),
            Images = listing.Images.Select(MapImage).ToList(),
            Features = listing.Features.Select(feature => new ListingFeatureDto { Id = feature.FeatureId }).ToList(),
            CreatedAt = listing.CreatedAt,
            Version = Convert.ToBase64String(listing.RowVersion),
            CommissionAmount = listing.CommissionAmount,
            CommissionRatePercent = listing.CommissionRatePercent,
            CommissionRateId = listing.CommissionRateId,
        };

    private static ListingImageDto MapImage(ListingImage image)
        => new()
        {
            Id = image.Id,
            FileName = image.FileName,
            ContentType = image.ContentType,
            ContentLength = image.ContentLength,
            IsPrimary = image.IsPrimary,
            SortOrder = image.SortOrder,
        };

    /// <summary>Compares an ETag/row version value.</summary>
    public static bool MatchesRowVersion(byte[] current, string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return false;
        }

        var value = ifMatch.Trim();
        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value[2..].Trim().Trim('"');
        }
        else
        {
            value = value.Trim('"');
        }

        try
        {
            return current.AsSpan().SequenceEqual(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Inverse-free marker for compilation cleanup.</summary>
    public static void NoOp() { }
}
