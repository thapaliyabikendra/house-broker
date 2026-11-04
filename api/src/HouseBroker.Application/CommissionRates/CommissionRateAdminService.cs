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
        var effectiveFrom = request.EffectiveFrom == default ? now : request.EffectiveFrom;

        await using var createTransaction = await _unitOfWork.BeginTransactionAsync(TransactionIsolationLevel.Serializable, cancellationToken);

        var rates = (await _repository.GetAllAsync(cancellationToken)).ToList();
        var candidate = CommissionRate.Create(request.MinPrice, request.MinInclusive, request.MaxPrice, request.MaxInclusive, request.RatePercent, request.IsActive, effectiveFrom);
        rates.Add(candidate);

        var violations = CommissionTierValidator.Validate(rates, now).ToList();
        if (effectiveFrom > now)
        {
            violations.AddRange(CommissionTierValidator.Validate(rates, effectiveFrom));
        }

        if (violations.Count > 0)
        {
            return Result.Failure<CommissionRateDto>(CommissionTierValidator.ToApplicationError(violations, candidate));
        }

        await _repository.AddAsync(candidate, cancellationToken);

        await _repository.AddAuditEventAsync(AuditEvent.Create(
            now,
            _currentUser.UserId,
            _currentUser.UserId.HasValue ? "User" : "System",
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
        await _cache.InvalidateCommissionRatesAsync(cancellationToken);

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

        var effectiveFrom = request.EffectiveFrom == default ? rate.EffectiveFrom : request.EffectiveFrom;
        var rates = (await _repository.GetAllAsync(cancellationToken)).ToList();

        // Adjacent tiers share a boundary, so moving this tier's boundary moves its neighbour's matching boundary.
        // Neighbours are found before the proposed tier replaces the stored one: the proposed tier has a new Id and
        // must never be matched as its own neighbour.
        var cascades = FindBoundaryCascades(rate, request, rates);

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
        var editedFieldByCopy = new Dictionary<CommissionRate, string>();
        var candidates = new List<CommissionRate>(rates);
        foreach (var cascade in cascades)
        {
            var copy = CommissionRate.Create(cascade.MinPrice, cascade.MinInclusive, cascade.MaxPrice, cascade.MaxInclusive, cascade.Tier.RatePercent, cascade.Tier.IsActive, cascade.Tier.EffectiveFrom);
            editedFieldByCopy[copy] = cascade.EditedField;
            candidates[candidates.FindIndex(item => ReferenceEquals(item, cascade.Tier))] = copy;
        }

        var violations = CommissionTierValidator.Validate(candidates, now).ToList();
        if (effectiveFrom > now)
        {
            violations.AddRange(CommissionTierValidator.Validate(candidates, effectiveFrom));
        }

        // A neighbour broken by this edit is reported against the edited tier's field, not as an existing-tier conflict.
        violations = violations
            .Select(violation => violation.Tier is not null && editedFieldByCopy.TryGetValue(violation.Tier, out var field)
                ? violation with { Tier = proposed, Field = field }
                : violation)
            .ToList();

        if (violations.Count > 0)
        {
            return Result.Failure<CommissionRateDto>(CommissionTierValidator.ToApplicationError(violations, proposed));
        }

        var oldValues = JsonSerializer.Serialize(ListingMapper.Instance.ToCommissionRate(rate));
        rate.Update(request.MinPrice, request.MinInclusive, request.MaxPrice, request.MaxInclusive, request.RatePercent, request.IsActive, effectiveFrom);
        await _repository.AddAuditEventAsync(BuildUpdateAudit(now, rate, oldValues), cancellationToken);

        foreach (var cascade in cascades)
        {
            var neighbourOldValues = JsonSerializer.Serialize(ListingMapper.Instance.ToCommissionRate(cascade.Tier));
            cascade.Tier.Update(cascade.MinPrice, cascade.MinInclusive, cascade.MaxPrice, cascade.MaxInclusive, cascade.Tier.RatePercent, cascade.Tier.IsActive, cascade.Tier.EffectiveFrom);
            await _repository.AddAuditEventAsync(BuildUpdateAudit(now, cascade.Tier, neighbourOldValues), cancellationToken);
        }

        var updateSave = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!updateSave.IsSuccess)
        {
            return Result.Failure<CommissionRateDto>(updateSave.Error!);
        }

        await updateTransaction.CommitAsync(cancellationToken);
        await _cache.InvalidateCommissionRatesAsync(cancellationToken);

        return Result.Success<CommissionRateDto>(ListingMapper.Instance.ToCommissionRate(rate));
    }

    /// <summary>
    /// Finds the active neighbours whose shared boundary must move with the edited tier: the successor when the max changes,
    /// the predecessor when the min changes. Open-ended edges have no neighbour and are left to the validator.
    /// </summary>
    private static List<BoundaryCascade> FindBoundaryCascades(CommissionRate rate, CommissionRateRequest request, IReadOnlyList<CommissionRate> rates)
    {
        var cascades = new List<BoundaryCascade>();

        if (rate.MaxPrice is decimal oldMax && request.MaxPrice is decimal newMax
            && (newMax != oldMax || request.MaxInclusive != rate.MaxInclusive))
        {
            cascades.AddRange(rates
                .Where(tier => tier.Id != rate.Id && tier.IsActive && tier.MinPrice == oldMax && tier.MinInclusive == !rate.MaxInclusive)
                .Select(tier => new BoundaryCascade(tier, newMax, !request.MaxInclusive, tier.MaxPrice, tier.MaxInclusive, nameof(CommissionRate.MaxPrice))));
        }

        if (request.MinPrice != rate.MinPrice || request.MinInclusive != rate.MinInclusive)
        {
            cascades.AddRange(rates
                .Where(tier => tier.Id != rate.Id && tier.IsActive && tier.MaxPrice == rate.MinPrice && tier.MaxInclusive == !rate.MinInclusive)
                .Select(tier => new BoundaryCascade(tier, tier.MinPrice, tier.MinInclusive, request.MinPrice, !request.MinInclusive, nameof(CommissionRate.MinPrice))));
        }

        return cascades;
    }

    private AuditEvent BuildUpdateAudit(DateTime now, CommissionRate rate, string oldValues)
        => AuditEvent.Create(
            now,
            _currentUser.UserId,
            _currentUser.UserId.HasValue ? "User" : "System",
            null,
            "commission.rate.updated",
            "CommissionRate",
            rate.Id,
            null,
            oldValues,
            JsonSerializer.Serialize(ListingMapper.Instance.ToCommissionRate(rate)));

    /// <summary>A neighbouring tier whose boundary follows the edited tier. <paramref name="EditedField"/> names the edited tier's field that caused it.</summary>
    private sealed record BoundaryCascade(CommissionRate Tier, decimal MinPrice, bool MinInclusive, decimal? MaxPrice, bool MaxInclusive, string EditedField);
}
