using HouseBroker.Application.Contracts.CommissionRates;
using HouseBroker.Application.Contracts;
using HouseBroker.Application.Listings;
using HouseBroker.Application.Mapping;
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
    IUnitOfWork unitOfWork) : ICommissionRateAdminService
{
    private readonly ICommissionRateRepository _repository = repository;
    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly TimeProvider _clock = clock;
    private readonly ICacheService _cache = cache;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    private string ActorType => _currentUser.UserId.HasValue ? "User" : "System";

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CommissionRateDto>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rates = await _repository.GetAllAsync(cancellationToken);
        return Result.Success<IReadOnlyList<CommissionRateDto>>(rates.Select(ListingMapper.Instance.ToCommissionRate).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<CommissionRateDto>> CreateAsync(CommissionRateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = _clock.GetUtcNow().UtcDateTime;
        var effectiveFrom = request.EffectiveFrom == default ? now : NormalizeUtc(request.EffectiveFrom);

        await using var createTransaction = await _unitOfWork.BeginTransactionAsync(TransactionIsolationLevel.Serializable, cancellationToken);

        var rates = (await _repository.GetAllAsync(cancellationToken)).ToList();
        var candidate = CommissionRate.Create(request.MinPrice, request.MinInclusive, request.MaxPrice, request.MaxInclusive, request.RatePercent, request.IsActive, effectiveFrom);
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

        await _repository.AddAuditEventAsync(AuditEvent.Create(
            now,
            _currentUser.UserId,
            ActorType,
            null,
            "commission.rate.created",
            "CommissionRate",
            candidate.Id,
            null,
            null,
            JsonSerializer.Serialize(new { request.MinPrice, request.MinInclusive, request.MaxPrice, request.MaxInclusive, request.RatePercent, request.IsActive, effectiveFrom })),
            cancellationToken);

        var createSave = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!createSave.IsSuccess)
        {
            return Result.Failure<CommissionRateDto>(createSave.Error!);
        }

        await createTransaction.CommitAsync(cancellationToken);

        // The write is durable at this point, so the invalidation must not be abandoned by a cancelled request.
        await _cache.InvalidateCommissionRatesAsync(CancellationToken.None);

        return Result.Success<CommissionRateDto>(ListingMapper.Instance.ToCommissionRate(candidate));
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

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return Result.Failure<CommissionRateDto>(new ApplicationError(ErrorCodes.PreconditionRequired, ErrorKind.PreconditionRequired));
        }

        if (!ListingService.MatchesRowVersion(rate.RowVersion, ifMatch))
        {
            return Result.Failure<CommissionRateDto>(new ApplicationError(ErrorCodes.PreconditionFailed, ErrorKind.PreconditionFailed));
        }

        var effectiveFrom = request.EffectiveFrom == default ? rate.EffectiveFrom : NormalizeUtc(request.EffectiveFrom);
        var rates = (await _repository.GetAllAsync(cancellationToken)).ToList();

        // Snapshot of the stored set, used to tell "this edit broke the set" from "the set was already broken".
        var storedRates = rates.ToList();

        // Adjacent tiers share a boundary, so moving this tier's boundary moves its neighbour's matching boundary.
        // Neighbours are found before the proposed tier replaces the stored one: the proposed tier has a new Id and
        // must never be matched as its own neighbour.
        var cascades = FindBoundaryCascades(rate, request, effectiveFrom, now, rates);

        var proposed = CommissionRate.Create(request.MinPrice, request.MinInclusive, request.MaxPrice, request.MaxInclusive, request.RatePercent, request.IsActive, effectiveFrom);
        var existingIndex = rates.FindIndex(item => item.Id == id);
        if (existingIndex >= 0)
        {
            rates[existingIndex] = proposed;
        }
        else
        {
            rates.Add(proposed);
        }

        // Validate against unsaved copies of the neighbours; tracked entities are only touched after validation passes.
        // Copies are matched by reference so entity equality overrides cannot confuse the lookup.
        var cascadeByCopy = new Dictionary<CommissionRate, (string EditedField, CommissionRate Original)>(ReferenceEqualityComparer.Instance);
        var candidates = new List<CommissionRate>(rates);
        foreach (var cascade in cascades)
        {
            var copy = CommissionRate.Create(cascade.MinPrice, cascade.MinInclusive, cascade.MaxPrice, cascade.MaxInclusive, cascade.Tier.RatePercent, cascade.Tier.IsActive, cascade.Tier.EffectiveFrom);
            cascadeByCopy[copy] = (cascade.EditedField, cascade.Tier);
            candidates[candidates.FindIndex(item => ReferenceEquals(item, cascade.Tier))] = copy;
        }

        // Validate now and at every later instant where a scheduled tier takes effect.
        var violations = CommissionTierValidator.ValidateTimeline(candidates, now);

        // A neighbour broken by this edit is reported against the edited tier's field, and the stored neighbour is kept
        // as a related tier so the client can see which record the edit collided with.
        violations = violations
            .Select(violation => violation.Tier is not null && cascadeByCopy.TryGetValue(violation.Tier, out var cascaded)
                ? violation with { Tier = proposed, Field = cascaded.EditedField, RelatedTier = cascaded.Original }
                : violation)
            .Distinct()
            .ToList();

        if (violations.Count > 0)
        {
            var storedSetInvalid = CommissionTierValidator.ValidateTimeline(storedRates, now).Count > 0;
            return Result.Failure<CommissionRateDto>(CommissionTierValidator.ToApplicationError(violations, proposed, storedSetInvalid));
        }

        var oldValues = JsonSerializer.Serialize(ListingMapper.Instance.ToCommissionRate(rate));
        rate.Update(request.MinPrice, request.MinInclusive, request.MaxPrice, request.MaxInclusive, request.RatePercent, request.IsActive, effectiveFrom);
        await AddUpdateAuditAsync(now, rate, "commission.rate.updated", oldValues, cancellationToken);

        foreach (var cascade in cascades)
        {
            var neighbourOldValues = JsonSerializer.Serialize(ListingMapper.Instance.ToCommissionRate(cascade.Tier));
            cascade.Tier.Update(cascade.MinPrice, cascade.MinInclusive, cascade.MaxPrice, cascade.MaxInclusive, cascade.Tier.RatePercent, cascade.Tier.IsActive, cascade.Tier.EffectiveFrom);
            await AddUpdateAuditAsync(now, cascade.Tier, "commission.rate.cascaded", neighbourOldValues, cancellationToken);
        }

        var updateSave = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!updateSave.IsSuccess)
        {
            return Result.Failure<CommissionRateDto>(updateSave.Error!);
        }

        await updateTransaction.CommitAsync(cancellationToken);

        // The write is durable at this point, so the invalidation must not be abandoned by a cancelled request.
        await _cache.InvalidateCommissionRatesAsync(CancellationToken.None);

        return Result.Success<CommissionRateDto>(ListingMapper.Instance.ToCommissionRate(rate));
    }

    /// <summary>
    /// Finds the active neighbours whose shared boundary must move with the edited tier: the successor when the max changes,
    /// the predecessor when the min changes. Open-ended edges have no neighbour and are left to the validator.
    /// Cascades only apply when the edited tier is part of the active chain both before and after the edit, and only
    /// to neighbours that are in effect now; an inactive or scheduled tier that merely shares a number with a live
    /// tier must not drag it along.
    /// </summary>
    private static List<BoundaryCascade> FindBoundaryCascades(
        CommissionRate rate,
        CommissionRateRequest request,
        DateTime effectiveFrom,
        DateTime now,
        IReadOnlyList<CommissionRate> rates)
    {
        var cascades = new List<BoundaryCascade>();

        if (!rate.IsActive || rate.EffectiveFrom > now || !request.IsActive || effectiveFrom > now)
        {
            return cascades;
        }

        bool IsLiveNeighbour(CommissionRate tier) => tier.Id != rate.Id && tier.IsActive && tier.EffectiveFrom <= now;

        if (rate.MaxPrice is decimal oldMax && request.MaxPrice is decimal newMax
            && (newMax != oldMax || request.MaxInclusive != rate.MaxInclusive))
        {
            cascades.AddRange(rates
                .Where(tier => IsLiveNeighbour(tier) && tier.MinPrice == oldMax && tier.MinInclusive == !rate.MaxInclusive)
                .Select(tier => new BoundaryCascade(tier, newMax, !request.MaxInclusive, tier.MaxPrice, tier.MaxInclusive, nameof(CommissionRate.MaxPrice))));
        }

        if (request.MinPrice != rate.MinPrice || request.MinInclusive != rate.MinInclusive)
        {
            cascades.AddRange(rates
                .Where(tier => IsLiveNeighbour(tier) && tier.MaxPrice == rate.MinPrice && tier.MaxInclusive == !rate.MinInclusive)
                .Select(tier => new BoundaryCascade(tier, tier.MinPrice, tier.MinInclusive, request.MinPrice, !request.MinInclusive, nameof(CommissionRate.MinPrice))));
        }

        // One cascade per neighbour: corrupt stored data can make a tier match both sides, and applying two
        // cascades to the same tier would lose the first edit and break the copy replacement during validation.
        return cascades.DistinctBy(cascade => cascade.Tier.Id).ToList();
    }

    /// <summary>
    /// Records an update audit event, skipping updates that leave the serialized tier unchanged.
    /// </summary>
    private async Task AddUpdateAuditAsync(DateTime now, CommissionRate rate, string action, string oldValues, CancellationToken cancellationToken)
    {
        var newValues = JsonSerializer.Serialize(ListingMapper.Instance.ToCommissionRate(rate));
        if (newValues == oldValues)
        {
            return;
        }

        await _repository.AddAuditEventAsync(
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
    }

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

    /// <summary>A neighbouring tier whose boundary follows the edited tier. <paramref name="EditedField"/> names the edited tier's field that caused it.</summary>
    private sealed record BoundaryCascade(CommissionRate Tier, decimal MinPrice, bool MinInclusive, decimal? MaxPrice, bool MaxInclusive, string EditedField);
}
