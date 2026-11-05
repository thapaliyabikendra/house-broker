using HouseBroker.Application.Contracts.CommissionRates;
using HouseBroker.Application.Contracts;
using HouseBroker.Application.Listings;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using System.Text.Json;

namespace HouseBroker.Application.CommissionRates;

/// <summary>Commission rate administration service implementation.</summary>
public sealed class CommissionRateAdminService(
    ICommissionRateRepository repository,
    ICurrentUserService currentUser,
    TimeProvider clock,
    ICacheService cache,
    IUnitOfWork unitOfWork,
    FluentValidation.IValidator<CommissionRateRequest> requestValidator) : ICommissionRateAdminService
{
    private readonly ICommissionRateRepository _repository = repository;
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly TimeProvider _clock = clock;
    private readonly ICacheService _cache = cache;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly FluentValidation.IValidator<CommissionRateRequest> _requestValidator = requestValidator;

    private string ActorType => _currentUser.UserId.HasValue ? "User" : "System";

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CommissionRateDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var rates = await _repository.GetAllAsync(cancellationToken);
        var active = CommissionTierSet.ActiveAt(rates, now);
        var bounds = BoundsById(active);

        // Retired bands keep their derived lower bound for display (computed from the full set, including
        // retired bands, ordered by upper bound) instead of being shown as "not applicable".
        foreach (var retired in rates.Where(rate => !rate.IsActive))
        {
            var fullOrdered = CommissionTierSet.Order(rates).ToList();
            var index = fullOrdered.FindIndex(tier => tier.Id == retired.Id);
            if (index >= 0)
            {
                var bound = CommissionTierSet.BoundsOf(fullOrdered, index);
                bounds[retired.Id] = (bound.Lower, bound.LowerInclusive);
            }
        }

        var dtos = CommissionTierSet.Order(rates)
            .Select(rate => bounds.TryGetValue(rate.Id, out var bound)
                ? ToDto(rate, bound.Lower, bound.LowerInclusive)
                : ToDto(rate, null, null))
            .ToList();

        return Result.Success<IReadOnlyList<CommissionRateDto>>(dtos);
    }

    /// <inheritdoc />
    public async Task<Result<CommissionRateDto>> CreateAsync(CommissionRateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = await _requestValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CommissionRateDto>(ValidationErrors.FromValidation(validation));
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var effectiveFrom = request.EffectiveFrom == default ? now : NormalizeUtc(request.EffectiveFrom);

        await using var createTransaction = await _unitOfWork.BeginTransactionAsync(TransactionIsolationLevel.Serializable, cancellationToken);

        var rates = (await _repository.GetAllForUpdateAsync(cancellationToken)).ToList();
        var candidate = CommissionRate.Create(request.MaxPrice, request.MaxInclusive, request.RatePercent, effectiveFrom);
        rates.Add(candidate);

        // Validate now and at every later instant where a scheduled tier takes effect.
        var violations = CommissionTierValidator.ValidateTimeline(rates, now);
        if (violations.Count > 0)
        {
            // Only a set that was already broken before this write is a state conflict; otherwise this request broke it.
            var storedSetInvalid = CommissionTierValidator
                .ValidateTimeline(rates.Where(item => !ReferenceEquals(item, candidate)), now)
                .Count > 0;
            return Result.Failure<CommissionRateDto>(CommissionTierValidator.ToApplicationError(violations, candidate, storedSetInvalid));
        }

        await _repository.AddAsync(candidate, cancellationToken);
        await AddAuditAsync(now, candidate, "commission.rate.created", null, SerializeBand(candidate), cancellationToken);

        var createSave = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!createSave.IsSuccess)
        {
            return Result.Failure<CommissionRateDto>(createSave.Error!);
        }

        await createTransaction.CommitAsync(cancellationToken);

        // The write is durable at this point, so the invalidation must not be abandoned by a cancelled request.
        await _cache.InvalidateCommissionRatesAsync(CancellationToken.None);

        return Result.Success(ToDtoWithDerivedBounds(candidate, rates, now));
    }

    /// <inheritdoc />
    public async Task<Result<CommissionRateDto>> UpdateAsync(Guid id, CommissionRateRequest request, string? ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = _clock.GetUtcNow().UtcDateTime;

        // The transaction opens before the row is loaded so the If-Match check and the write see the same committed state.
        await using var updateTransaction = await _unitOfWork.BeginTransactionAsync(TransactionIsolationLevel.Serializable, cancellationToken);

        var rate = await _repository.GetByIdAsync(id, cancellationToken);
        if (rate is null)
        {
            return Result.Failure<CommissionRateDto>(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        var precondition = CheckIfMatch(rate, ifMatch);
        if (precondition is not null)
        {
            return Result.Failure<CommissionRateDto>(precondition);
        }

        var validation = await _requestValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<CommissionRateDto>(ValidationErrors.FromValidation(validation));
        }

        var effectiveFrom = request.EffectiveFrom == default ? rate.EffectiveFrom : NormalizeUtc(request.EffectiveFrom);
        var rates = (await _repository.GetAllForUpdateAsync(cancellationToken)).ToList();

        // Snapshot of the stored set, used to tell "this edit broke the set" from "the set was already broken".
        var storedRates = rates.ToList();

        var proposed = CommissionRate.Create(request.MaxPrice, request.MaxInclusive, request.RatePercent, effectiveFrom);
        if (!rate.IsActive)
        {
            proposed.Retire();
        }

        var existingIndex = rates.FindIndex(item => item.Id == id);
        if (existingIndex >= 0)
        {
            rates[existingIndex] = proposed;
        }
        else
        {
            rates.Add(proposed);
        }

        // Validate now and at every later instant where a scheduled tier takes effect.
        var violations = CommissionTierValidator.ValidateTimeline(rates, now);
        if (violations.Count > 0)
        {
            var storedSetInvalid = CommissionTierValidator.ValidateTimeline(storedRates, now).Count > 0;
            return Result.Failure<CommissionRateDto>(CommissionTierValidator.ToApplicationError(violations, proposed, storedSetInvalid));
        }

        var oldValues = SerializeBand(rate);
        var normalizedMaxInclusive = request.MaxPrice is null ? false : request.MaxInclusive;
        var isNoOp = rate.MaxPrice == request.MaxPrice
            && rate.MaxInclusive == normalizedMaxInclusive
            && rate.RatePercent == request.RatePercent
            && rate.EffectiveFrom == effectiveFrom;

        rate.Update(request.MaxPrice, request.MaxInclusive, request.RatePercent, effectiveFrom);
        if (!isNoOp)
        {
            await AddAuditAsync(now, rate, "commission.rate.updated", oldValues, SerializeBand(rate), cancellationToken);
        }

        var updateSave = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!updateSave.IsSuccess)
        {
            return Result.Failure<CommissionRateDto>(updateSave.Error!);
        }

        await updateTransaction.CommitAsync(cancellationToken);

        // The write is durable at this point, so the invalidation must not be abandoned by a cancelled request.
        await _cache.InvalidateCommissionRatesAsync(CancellationToken.None);

        var finalSet = rates.Select(item => ReferenceEquals(item, proposed) ? rate : item).ToList();
        return Result.Success(ToDtoWithDerivedBounds(rate, finalSet, now));
    }

    /// <inheritdoc />
    public async Task<Result> RetireAsync(Guid id, string? ifMatch, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        await using var retireTransaction = await _unitOfWork.BeginTransactionAsync(TransactionIsolationLevel.Serializable, cancellationToken);

        var rate = await _repository.GetByIdAsync(id, cancellationToken);
        if (rate is null)
        {
            return Result.Failure(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        var precondition = CheckIfMatch(rate, ifMatch);
        if (precondition is not null)
        {
            return Result.Failure(precondition);
        }

        if (!rate.IsActive)
        {
            return Result.Success();
        }

        var rates = (await _repository.GetAllForUpdateAsync(cancellationToken)).ToList();

        var retiredCopy = Copy(rate);
        retiredCopy.Retire();
        Replace(rates, rate.Id, retiredCopy);

        // Retiring the active open-ended top leaves no band covering the top of the range; promote the highest
        // remaining band that is effective now so the set stays exhaustive.
        CommissionRate? promoted = null;
        CommissionRate? promotedCopy = null;
        if (IsActiveOpenEndedTop(rate, now))
        {
            var remaining = CommissionTierSet.ActiveAt(rates.Where(item => item.Id != rate.Id), now);
            if (remaining.Count > 0 && remaining[^1].MaxPrice is not null)
            {
                promoted = remaining[^1];
                promotedCopy = Copy(promoted);
                promotedCopy.PromoteToOpenEnded();
                Replace(rates, promoted.Id, promotedCopy);
            }
        }

        var violations = CommissionTierValidator.ValidateTimeline(rates, now);
        if (violations.Count > 0)
        {
            // Retiring has no request fields to blame: a request that would leave the stored set invalid is a state conflict.
            return Result.Failure(CommissionTierValidator.ToConflict(violations));
        }

        var retiredOldValues = SerializeBand(rate);
        rate.Retire();
        await AddAuditAsync(now, rate, "commission.rate.retired", retiredOldValues, SerializeBand(rate), cancellationToken);

        if (promoted is not null)
        {
            var promotedOldValues = SerializeBand(promoted);
            promoted.PromoteToOpenEnded();
            await AddAuditAsync(now, promoted, "commission.rate.promoted", promotedOldValues, SerializeBand(promoted), cancellationToken);
        }

        var retireSave = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!retireSave.IsSuccess)
        {
            return Result.Failure(retireSave.Error!);
        }

        await retireTransaction.CommitAsync(cancellationToken);

        // The write is durable at this point, so the invalidation must not be abandoned by a cancelled request.
        await _cache.InvalidateCommissionRatesAsync(CancellationToken.None);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<CommissionRateDto>> ReactivateAsync(Guid id, string? ifMatch, CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        await using var reactivateTransaction = await _unitOfWork.BeginTransactionAsync(TransactionIsolationLevel.Serializable, cancellationToken);

        var rate = await _repository.GetByIdAsync(id, cancellationToken);
        if (rate is null)
        {
            return Result.Failure<CommissionRateDto>(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound));
        }

        var precondition = CheckIfMatch(rate, ifMatch);
        if (precondition is not null)
        {
            return Result.Failure<CommissionRateDto>(precondition);
        }

        if (rate.IsActive)
        {
            var currentRates = await _repository.GetAllAsync(cancellationToken);
            return Result.Success(ToDtoWithDerivedBounds(rate, currentRates, now));
        }

        var rates = (await _repository.GetAllForUpdateAsync(cancellationToken)).ToList();

        var reactivatedCopy = Copy(rate);
        reactivatedCopy.Reactivate();
        Replace(rates, rate.Id, reactivatedCopy);

        var violations = CommissionTierValidator.ValidateTimeline(rates, now);
        if (violations.Count > 0)
        {
            // Reactivating has no request fields to blame: a request that would leave the stored set invalid is a state conflict.
            return Result.Failure<CommissionRateDto>(CommissionTierValidator.ToConflict(violations));
        }

        var oldValues = SerializeBand(rate);
        rate.Reactivate();
        await AddAuditAsync(now, rate, "commission.rate.reactivated", oldValues, SerializeBand(rate), cancellationToken);

        var reactivateSave = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!reactivateSave.IsSuccess)
        {
            return Result.Failure<CommissionRateDto>(reactivateSave.Error!);
        }

        await reactivateTransaction.CommitAsync(cancellationToken);

        // The write is durable at this point, so the invalidation must not be abandoned by a cancelled request.
        await _cache.InvalidateCommissionRatesAsync(CancellationToken.None);

        var finalSet = rates.Select(item => ReferenceEquals(item, reactivatedCopy) ? rate : item).ToList();
        return Result.Success(ToDtoWithDerivedBounds(rate, finalSet, now));
    }

    private static Dictionary<Guid, (decimal Lower, bool LowerInclusive)> BoundsById(IReadOnlyList<CommissionRate> orderedActive)
    {
        var bounds = new Dictionary<Guid, (decimal Lower, bool LowerInclusive)>(orderedActive.Count);
        for (var index = 0; index < orderedActive.Count; index++)
        {
            bounds[orderedActive[index].Id] = CommissionTierSet.BoundsOf(orderedActive, index);
        }

        return bounds;
    }

    private static CommissionRateDto ToDtoWithDerivedBounds(CommissionRate rate, IEnumerable<CommissionRate> set, DateTime now)
    {
        if (!rate.IsActive)
        {
            // Retired bands keep their derived lower bound for display (computed from the full set).
            var fullOrdered = CommissionTierSet.Order(set).ToList();
            var retiredIndex = fullOrdered.FindIndex(tier => tier.Id == rate.Id);
            if (retiredIndex < 0)
            {
                return ToDto(rate, null, null);
            }

            var retiredBound = CommissionTierSet.BoundsOf(fullOrdered, retiredIndex);
            return ToDto(rate, retiredBound.Lower, retiredBound.LowerInclusive);
        }

        var active = CommissionTierSet.ActiveAt(set, now);
        var index = active.ToList().FindIndex(tier => tier.Id == rate.Id);
        return index < 0
            ? ToDto(rate, null, null)
            : ToDto(rate, CommissionTierSet.BoundsOf(active, index).Lower, CommissionTierSet.BoundsOf(active, index).LowerInclusive);
    }

    private static CommissionRateDto ToDto(CommissionRate rate, decimal? minPrice, bool? minInclusive)
        => new()
        {
            Id = rate.Id,
            MinPrice = minPrice,
            MinInclusive = minInclusive,
            MaxPrice = rate.MaxPrice,
            MaxInclusive = rate.MaxInclusive,
            RatePercent = rate.RatePercent,
            IsActive = rate.IsActive,
            EffectiveFrom = rate.EffectiveFrom,
            Version = Convert.ToBase64String(rate.RowVersion),
        };

    private static CommissionRate Copy(CommissionRate rate) => rate.DetachedCopy();

    private static void Replace(List<CommissionRate> rates, Guid id, CommissionRate replacement)
    {
        var index = rates.FindIndex(item => item.Id == id);
        if (index >= 0)
        {
            rates[index] = replacement;
        }
        else
        {
            rates.Add(replacement);
        }
    }

    private static bool IsActiveOpenEndedTop(CommissionRate rate, DateTime now)
        => rate.IsActive && rate.EffectiveFrom <= now && rate.MaxPrice is null;

    private static ApplicationError? CheckIfMatch(CommissionRate rate, string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return new ApplicationError(ErrorCodes.PreconditionRequired, ErrorKind.PreconditionRequired);
        }

        return ListingService.MatchesRowVersion(rate.RowVersion, ifMatch)
            ? null
            : new ApplicationError(ErrorCodes.PreconditionFailed, ErrorKind.PreconditionFailed);
    }

    private static string SerializeBand(CommissionRate rate)
        => JsonSerializer.Serialize(new { rate.MaxPrice, rate.MaxInclusive, rate.RatePercent, rate.EffectiveFrom, rate.IsActive });

    private async Task AddAuditAsync(DateTime now, CommissionRate rate, string action, string? oldValues, string? newValues, CancellationToken cancellationToken)
        => await _repository.AddAuditEventAsync(
            AuditEvent.Create(
                now,
                _currentUser.UserId,
                ActorType,
                null,
                action,
                "CommissionRate",
                rate.Id,
                null,
                oldValues,
                newValues),
            cancellationToken);

    /// <summary>
    /// Normalizes a client-supplied instant to UTC. Local values are converted; unspecified values are taken as UTC,
    /// which is what the API contract documents for timestamps.
    /// </summary>
    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };
}
