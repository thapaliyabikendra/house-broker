using HouseBroker.Application.Contracts.CommissionRates;
using HouseBroker.Application.Contracts;
using HouseBroker.Application.Mapping;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using System.Text.Json;

namespace HouseBroker.Application.Services;

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

        var now = _clock.GetUtcNow().UtcDateTime;
        var effectiveFrom = request.EffectiveFrom == default ? rate.EffectiveFrom : request.EffectiveFrom;

        await using var updateTransaction = await _unitOfWork.BeginTransactionAsync(TransactionIsolationLevel.Serializable, cancellationToken);

        var rates = (await _repository.GetAllAsync(cancellationToken)).ToList();
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

        var violations = CommissionTierValidator.Validate(rates, now).ToList();
        if (effectiveFrom > now)
        {
            violations.AddRange(CommissionTierValidator.Validate(rates, effectiveFrom));
        }

        if (violations.Count > 0)
        {
            return Result.Failure<CommissionRateDto>(CommissionTierValidator.ToApplicationError(violations, proposed));
        }

        var oldValues = JsonSerializer.Serialize(ListingMapper.Instance.ToCommissionRate(rate));

        rate.Update(request.MinPrice, request.MinInclusive, request.MaxPrice, request.MaxInclusive, request.RatePercent, request.IsActive, effectiveFrom);

        await _repository.AddAuditEventAsync(AuditEvent.Create(
            now,
            _currentUser.UserId,
            _currentUser.UserId.HasValue ? "User" : "System",
            null,
            "commission.rate.updated",
            "CommissionRate",
            rate.Id,
            null,
            oldValues,
            JsonSerializer.Serialize(ListingMapper.Instance.ToCommissionRate(rate))), cancellationToken);

        var updateSave = await _unitOfWork.SaveChangesAsync(cancellationToken);
        if (!updateSave.IsSuccess)
        {
            return Result.Failure<CommissionRateDto>(updateSave.Error!);
        }

        await updateTransaction.CommitAsync(cancellationToken);
        await _cache.InvalidateCommissionRatesAsync(cancellationToken);

        return Result.Success<CommissionRateDto>(ListingMapper.Instance.ToCommissionRate(rate));
    }

}



