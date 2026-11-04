using HouseBroker.Application.Contracts;
using HouseBroker.Application.Mapping;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using Microsoft.Extensions.Logging;
using System.Text;

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
    FluentValidation.IValidator<HouseBroker.Application.Contracts.UpdateListingRequest> updateValidator,
    ILogger<ListingService> logger) : IListingService
{
    private readonly IListingRepository _repository = repository;
    private readonly IListingQueries _queries = queries;
    private readonly ICommissionRateProvider _commissionRates = commissionRates;
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _clock = clock;
    private readonly FluentValidation.IValidator<HouseBroker.Application.Contracts.CreateListingRequest> _createValidator = createValidator;
    private readonly FluentValidation.IValidator<HouseBroker.Application.Contracts.UpdateListingRequest> _updateValidator = updateValidator;
    private readonly ILogger<ListingService> _logger = logger;

    /// <inheritdoc />
    public async Task<Result<OwnerListingDto>> CreateAsync(CreateListingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            _logger.LogWarning("Listing creation was rejected because the caller is not an authenticated broker.");
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var syncResult = await SyncBrokerProfileAsync(brokerId.Value, cancellationToken);
        if (!syncResult.IsSuccess)
        {
            return Result.Failure<OwnerListingDto>(syncResult.Error!);
        }

        NormalizeRequest(request);

        var createValidation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!createValidation.IsValid)
        {
            var failure = createValidation.Errors[0];
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.ValidationFailed, ErrorKind.Validation, failure.PropertyName));
        }


        var featureIds = request.FeatureIds
            .Where(featureId => featureId != Guid.Empty)
            .Distinct()
            .ToList();
        var featureLookup = await GetFeatureLookupAsync(featureIds, cancellationToken);
        if (featureIds.Any(featureId => !featureLookup.ContainsKey(featureId)))
        {
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.ValidationFailed, ErrorKind.Validation, nameof(CreateListingRequest.FeatureIds)));
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

        listing.ReplaceFeatures(featureIds);

        var tiers = await _commissionRates.GetActiveAsync(_clock.GetUtcNow().UtcDateTime, cancellationToken);
        var commission = CommissionCalculator.Calculate(request.Price, tiers, _clock.GetUtcNow().UtcDateTime);
        listing.ApplyCommission(commission.Amount, commission.RatePercent, commission.RateId);

        await _repository.AddAsync(listing, cancellationToken);
        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!saveResult.IsSuccess)
        {
            return Result.Failure<OwnerListingDto>(saveResult.Error!);
        }

        _logger.LogInformation("Listing {ListingId} created by broker {BrokerId}.", listing.Id, brokerId.Value);

        return Result.Success<OwnerListingDto>(MapOwner(listing, featureLookup));
    }

    /// <inheritdoc />
    public async Task<Result<OwnerListingDto>> UpdateAsync(Guid id, UpdateListingRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            _logger.LogWarning("Access to listing {ListingId} was rejected because the caller is not an authenticated broker.", id);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var syncResult = await SyncBrokerProfileAsync(brokerId.Value, cancellationToken);
        if (!syncResult.IsSuccess)
        {
            return Result.Failure<OwnerListingDto>(syncResult.Error!);
        }

        var listing = await _repository.GetByIdAsync(id, cancellationToken);
        if (listing is null)
        {
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (listing.BrokerId != brokerId.Value)
        {
            _logger.LogWarning("Access to listing {ListingId} by broker {BrokerId} was rejected because the caller is not the owner.", id, brokerId.Value);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        NormalizeRequest(request);

        var updateValidation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!updateValidation.IsValid)
        {
            var failure = updateValidation.Errors[0];
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.ValidationFailed, ErrorKind.Validation, failure.PropertyName));
        }

        var featureIds = request.FeatureIds
            .Where(featureId => featureId != Guid.Empty)
            .Distinct()
            .ToList();
        var featureLookup = await GetFeatureLookupAsync(featureIds, cancellationToken);
        if (featureIds.Any(featureId => !featureLookup.ContainsKey(featureId)))
        {
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.ValidationFailed, ErrorKind.Validation, nameof(UpdateListingRequest.FeatureIds)));
        }

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            _logger.LogWarning("Update of listing {ListingId} by broker {BrokerId} was rejected because If-Match was missing.", id, brokerId.Value);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.PreconditionRequired, ErrorKind.PreconditionRequired));
        }

        if (!MatchesRowVersion(listing.RowVersion, ifMatch))
        {
            _logger.LogWarning("Update of listing {ListingId} by broker {BrokerId} was rejected because If-Match was stale.", id, brokerId.Value);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.PreconditionFailed, ErrorKind.PreconditionFailed));
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

        listing.ReplaceFeatures(featureIds);
        listing.Touch(_clock.GetUtcNow().UtcDateTime, brokerId.Value);

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

        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!saveResult.IsSuccess)
        {
            return Result.Failure<OwnerListingDto>(saveResult.Error!);
        }
        _logger.LogInformation("Listing {ListingId} updated by broker {BrokerId}.", listing.Id, brokerId.Value);
        return Result.Success<OwnerListingDto>(MapOwner(listing, featureLookup));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            _logger.LogWarning("Listing deletion was rejected because the caller is not an authenticated broker.");
            return Result.Failure(new ApplicationError(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var listing = await _repository.GetByIdAsync(id, cancellationToken);
        if (listing is null)
        {
            return Result.Failure(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (listing.BrokerId != brokerId.Value)
        {
            _logger.LogWarning("Deletion of listing {ListingId} by broker {BrokerId} was rejected because the caller is not the owner.", id, brokerId.Value);
            return Result.Failure(new ApplicationError(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        listing.SoftDelete();
        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!saveResult.IsSuccess)
        {
            return Result.Failure(saveResult.Error!);
        }
        _logger.LogInformation("Listing {ListingId} deleted by broker {BrokerId}.", id, brokerId.Value);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<ListingDetailDto>> GetDetailAsync(Guid id, bool includeContact, CancellationToken cancellationToken)
    {
        var listing = await _queries.GetPublicAsync(id, cancellationToken);
        if (listing is null || listing.Status != ListingStatus.Active)
        {
            return Result.Failure<ListingDetailDto>(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
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

        return Result.Success<ListingDetailDto>(dto);
    }

    /// <inheritdoc />
    public async Task<Result<OwnerListingDto>> GetOwnerDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            _logger.LogWarning("Access to listing {ListingId} was rejected because the caller is not an authenticated broker.", id);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var listing = await _repository.GetByIdAsync(id, cancellationToken);
        if (listing is null)
        {
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (listing.BrokerId != brokerId.Value)
        {
            _logger.LogWarning("Access to listing {ListingId} by broker {BrokerId} was rejected because the caller is not the owner.", id, brokerId.Value);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        return Result.Success<OwnerListingDto>(MapOwner(listing));
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<ListingSummaryDto>>> SearchAsync(ListingSearchQuery query, CancellationToken cancellationToken)
    {
        if (query is null)
        {
            return Result.Failure<PagedResult<ListingSummaryDto>>(new ApplicationError(ErrorCodes.Required, ErrorKind.Validation, nameof(query)));
        }

        if (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice.Value > query.MaxPrice.Value)
        {
            return Result.Failure<PagedResult<ListingSummaryDto>>(new ApplicationError(ErrorCodes.OutOfRange, ErrorKind.Validation, nameof(query.MinPrice)));
        }

        if (query.Location is not null)
        {
            query.Location = NormalizeText(query.Location);
        }

        if (!string.IsNullOrWhiteSpace(query.Location) && query.Location.Trim().Length > HouseBroker.Domain.Common.Limits.MaxSearchTextLength)
        {
            return Result.Failure<PagedResult<ListingSummaryDto>>(new ApplicationError(ErrorCodes.OutOfRange, ErrorKind.Validation, nameof(query.Location)));
        }

        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, HouseBroker.Domain.Common.Limits.MaxPageSize);
        if (query.PageSize == 0)
        {
            query.PageSize = HouseBroker.Domain.Common.Limits.DefaultPageSize;
        }

        var result = await _queries.SearchAsync(query, cancellationToken);
        return Result.Success<PagedResult<ListingSummaryDto>>(result);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<OwnerListingDto>>> GetMineAsync(CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            _logger.LogWarning("Listing retrieval was rejected because the caller is not an authenticated broker.");
            return Result.Failure<IReadOnlyList<OwnerListingDto>>(new ApplicationError(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var listings = await _queries.GetMineAsync(brokerId.Value, cancellationToken);
        return Result.Success<IReadOnlyList<OwnerListingDto>>(listings.Select(listing => MapOwner(listing)).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<OwnerListingDto>> PublishAsync(Guid id, CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            _logger.LogWarning("Access to listing {ListingId} was rejected because the caller is not an authenticated broker.", id);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var listing = await _repository.GetByIdAsync(id, cancellationToken);
        if (listing is null)
        {
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (listing.BrokerId != brokerId.Value)
        {
            _logger.LogWarning("Access to listing {ListingId} by broker {BrokerId} was rejected because the caller is not the owner.", id, brokerId.Value);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        var fromStatus = listing.Status;
        listing.Publish();
        await _repository.AddStatusHistoryAsync(ListingStatusHistory.Create(listing.Id, fromStatus, listing.Status, brokerId.Value, _clock.GetUtcNow().UtcDateTime, null), cancellationToken);
        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!saveResult.IsSuccess)
        {
            return Result.Failure<OwnerListingDto>(saveResult.Error!);
        }
        return Result.Success<OwnerListingDto>(MapOwner(listing));
    }

    /// <inheritdoc />
    public async Task<Result<OwnerListingDto>> MarkSoldAsync(Guid id, CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            _logger.LogWarning("Access to listing {ListingId} was rejected because the caller is not an authenticated broker.", id);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var listing = await _repository.GetByIdAsync(id, cancellationToken);
        if (listing is null)
        {
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (listing.BrokerId != brokerId.Value)
        {
            _logger.LogWarning("Access to listing {ListingId} by broker {BrokerId} was rejected because the caller is not the owner.", id, brokerId.Value);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        var fromStatus = listing.Status;
        listing.MarkSold();
        await _repository.AddStatusHistoryAsync(ListingStatusHistory.Create(listing.Id, fromStatus, listing.Status, brokerId.Value, _clock.GetUtcNow().UtcDateTime, null), cancellationToken);
        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!saveResult.IsSuccess)
        {
            return Result.Failure<OwnerListingDto>(saveResult.Error!);
        }
        return Result.Success<OwnerListingDto>(MapOwner(listing));
    }

    /// <inheritdoc />
    public async Task<Result<OwnerListingDto>> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var brokerId = _currentUser.UserId;
        if (brokerId is null)
        {
            _logger.LogWarning("Access to listing {ListingId} was rejected because the caller is not an authenticated broker.", id);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.Forbidden, ErrorKind.Forbidden));
        }

        var listing = await _repository.GetByIdAsync(id, cancellationToken);
        if (listing is null)
        {
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        if (listing.BrokerId != brokerId.Value)
        {
            _logger.LogWarning("Access to listing {ListingId} by broker {BrokerId} was rejected because the caller is not the owner.", id, brokerId.Value);
            return Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.NotOwner, ErrorKind.Forbidden));
        }

        var fromStatus = listing.Status;
        listing.Archive();
        if (fromStatus != listing.Status)
        {
            await _repository.AddStatusHistoryAsync(ListingStatusHistory.Create(listing.Id, fromStatus, listing.Status, brokerId.Value, _clock.GetUtcNow().UtcDateTime, null), cancellationToken);
        }

        var saveResult = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!saveResult.IsSuccess)
        {
            return Result.Failure<OwnerListingDto>(saveResult.Error!);
        }
        return Result.Success<OwnerListingDto>(MapOwner(listing));
    }

    private async Task<Result> SyncBrokerProfileAsync(Guid brokerId, CancellationToken cancellationToken)
    {
        var fullName = _currentUser.FullName;
        var email = _currentUser.Email;
        var phone = _currentUser.PhoneNumber;

        if (string.IsNullOrEmpty(fullName))
        {
            _logger.LogWarning("Broker profile synchronization failed for broker {BrokerId} due to missing required name claim.", brokerId);
            return Result.Failure(new ApplicationError(ErrorCodes.Forbidden, ErrorKind.Forbidden, null, "insufficient_scope"));
        }

        var profile = BrokerProfile.Create(brokerId, fullName, email, phone);
        await _queries.UpsertBrokerProfileAsync(profile, cancellationToken);
        return Result.Success();
    }

    private static ListingDetailDto MapDetail(Listing listing)
    {
        return ListingMapper.Instance.ToDetail(listing);
    }

    private static OwnerListingDto MapOwner(Listing listing, IReadOnlyDictionary<Guid, Feature>? featureLookup = null)
    {
        var dto = ListingMapper.Instance.ToOwner(listing);
        if (featureLookup is not null)
        {
            foreach (var featureDto in dto.Features)
            {
                if (featureLookup.TryGetValue(featureDto.Id, out var feature))
                {
                    featureDto.Code = feature.Code;
                    featureDto.Name = feature.Name;
                }
            }
        }
        return dto;
    }

    private async Task<IReadOnlyDictionary<Guid, Feature>> GetFeatureLookupAsync(
        List<Guid> featureIds,
        CancellationToken cancellationToken)
    {
        if (featureIds.Count == 0)
        {
            return new Dictionary<Guid, Feature>();
        }

        var features = await _queries.GetFeaturesAsync(cancellationToken);
        return features.ToDictionary(feature => feature.Id);
    }

    private static void NormalizeRequest(CreateListingRequest request)
    {
        request.Title = NormalizeText(request.Title);
        request.Description = NormalizeText(request.Description);
        request.Province = NormalizeText(request.Province);
        request.City = NormalizeText(request.City);
        request.Locality = NormalizeText(request.Locality);
        request.PostalCode = NormalizeText(request.PostalCode);
    }

    private static void NormalizeRequest(UpdateListingRequest request)
    {
        request.Title = NormalizeText(request.Title);
        request.Description = NormalizeText(request.Description);
        request.Province = NormalizeText(request.Province);
        request.City = NormalizeText(request.City);
        request.Locality = NormalizeText(request.Locality);
        request.PostalCode = NormalizeText(request.PostalCode);
    }

    private static string NormalizeText(string? value)
        => string.IsNullOrEmpty(value) ? string.Empty : value.Normalize(NormalizationForm.FormC).Trim();

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
