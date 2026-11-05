using HouseBroker.Application.Contracts;
using HouseBroker.Application.Contracts.CommissionRates;
using HouseBroker.Application.CommissionRates;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using Moq;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Commission band administration tests for the upper-bound model and soft retire.</summary>
public sealed class CommissionRateAdminServiceTests
{
    private static readonly byte[] RowVersion = [1, 2, 3, 4];
    private static readonly string IfMatch = Convert.ToBase64String(RowVersion);

    /// <summary>A new band is validated against the whole set, persisted and audited.</summary>
    [Fact]
    public async Task CreateAsync_ValidBand_PersistsAndAudits()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.CreateAsync(Request(15_000_000m, false, 1.25m), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, fixture.Tiers.Count);
        var created = fixture.Tiers[^1];
        Assert.Equal(15_000_000m, created.MaxPrice);
        Assert.True(created.IsActive);
        var audit = Assert.Single(fixture.Audits);
        Assert.Equal("commission.rate.created", audit.Action);
        Assert.Equal(created.Id, audit.EntityId);
        Assert.NotNull(audit.NewValues);
        Assert.Contains("15000000", audit.NewValues, StringComparison.Ordinal);
        Assert.DoesNotContain("MinPrice", audit.NewValues, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MinInclusive", audit.NewValues, StringComparison.OrdinalIgnoreCase);
        fixture.Cache.Verify(cache => cache.InvalidateCommissionRatesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Transaction.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A duplicate maximum is rejected on the new band's MaxPrice field.</summary>
    [Fact]
    public async Task CreateAsync_DuplicateMax_FailsValidationAndPersistsNothing()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.CreateAsync(Request(10_000_000m, true, 1.25m), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.Error!.Code);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        var fieldError = Assert.Single(result.Error.FieldErrors);
        Assert.Equal("MaxPrice", fieldError.Field);
        Assert.Equal(ErrorCodes.TierMaxNotAscending, fieldError.Code);
        Assert.Equal(3, fixture.Tiers.Count);
        Assert.Empty(fixture.Audits);
        fixture.Repository.Verify(repo => repo.AddAsync(It.IsAny<CommissionRate>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.Transaction.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A future band that would break the set when it takes effect is rejected.</summary>
    [Fact]
    public async Task CreateAsync_FutureBandBreakingTimeline_FailsValidation()
    {
        var fixture = Fixture.Create();
        var future = TimeProvider.System.GetUtcNow().UtcDateTime.AddDays(1);

        var result = await fixture.Service.CreateAsync(Request(null, false, 1.25m, future), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        var fieldError = Assert.Single(result.Error.FieldErrors);
        Assert.Equal("MaxPrice", fieldError.Field);
        Assert.Equal(ErrorCodes.TierOpenEndedCount, fieldError.Code);
        Assert.Equal(3, fixture.Tiers.Count);
        Assert.Empty(fixture.Audits);
    }

    /// <summary>The request validator rejects a rate outside (0, 100] before the domain guard.</summary>
    [Fact]
    public async Task CreateAsync_InvalidRate_FailsRequestValidation()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.CreateAsync(Request(15_000_000m, false, 101m), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        var fieldError = Assert.Single(result.Error.FieldErrors);
        Assert.Equal("RatePercent", fieldError.Field);
        Assert.Equal(ErrorCodes.OutOfRange, fieldError.Code);
        Assert.Empty(fixture.Audits);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>PUT without If-Match is a 428 precondition-required failure.</summary>
    [Fact]
    public async Task UpdateAsync_MissingIfMatch_ReturnsPreconditionRequired()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(6_000_000m, false, 2m), null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PreconditionRequired, result.Error!.Code);
        Assert.Equal(ErrorKind.PreconditionRequired, result.Error.Kind);
        Assert.Equal(5_000_000m, fixture.A.MaxPrice);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A stale If-Match is a 412 precondition-failed failure and nothing is written.</summary>
    [Fact]
    public async Task UpdateAsync_StaleIfMatch_ReturnsPreconditionFailed()
    {
        var fixture = Fixture.Create();
        var stale = Convert.ToBase64String([9, 9, 9]);

        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(6_000_000m, false, 2m), stale, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PreconditionFailed, result.Error!.Code);
        Assert.Equal(ErrorKind.PreconditionFailed, result.Error.Kind);
        Assert.Equal(5_000_000m, fixture.A.MaxPrice);
        Assert.Empty(fixture.Audits);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An update mutates the tracked row (regression: tracked read, not AsNoTracking).</summary>
    [Fact]
    public async Task UpdateAsync_ChangesTrackedBandAndAudits()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(6_000_000m, false, 2.25m), IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(6_000_000m, fixture.A.MaxPrice);
        Assert.Equal(2.25m, fixture.A.RatePercent);
        var audit = Assert.Single(fixture.Audits);
        Assert.Equal("commission.rate.updated", audit.Action);
        Assert.Equal(fixture.A.Id, audit.EntityId);
        Assert.NotNull(audit.OldValues);
        Assert.NotNull(audit.NewValues);
        Assert.Contains("5000000", audit.OldValues, StringComparison.Ordinal);
        Assert.Contains("6000000", audit.NewValues, StringComparison.Ordinal);
        Assert.DoesNotContain("MinPrice", audit.NewValues, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MinInclusive", audit.NewValues, StringComparison.OrdinalIgnoreCase);
        fixture.Cache.Verify(cache => cache.InvalidateCommissionRatesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Transaction.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An update that changes nothing does not write an audit event.</summary>
    [Fact]
    public async Task UpdateAsync_NoOp_SkipsAudit()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(5_000_000m, false, 2m), IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(fixture.Audits);
    }

    /// <summary>A duplicate maximum caused by the edited band is reported on that band's MaxPrice field.</summary>
    [Fact]
    public async Task UpdateAsync_DuplicateMax_ReportsEditedFieldAndPersistsNothing()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(10_000_000m, true, 2m), IfMatch, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        var fieldError = Assert.Single(result.Error.FieldErrors);
        Assert.Equal("MaxPrice", fieldError.Field);
        Assert.Equal(ErrorCodes.TierMaxNotAscending, fieldError.Code);
        Assert.Equal(5_000_000m, fixture.A.MaxPrice);
        Assert.Empty(fixture.Audits);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.Transaction.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Updating an unknown band is a 404.</summary>
    [Fact]
    public async Task UpdateAsync_UnknownId_ReturnsNotFound()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.UpdateAsync(Guid.CreateVersion7(), Request(6_000_000m, false, 2m), IfMatch, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.Error!.Code);
        Assert.Equal(ErrorKind.NotFound, result.Error.Kind);
    }

    /// <summary>Retiring a non-top band keeps the open-ended top band.</summary>
    [Fact]
    public async Task RetireAsync_NonTopBand_RetiresWithoutPromotion()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.RetireAsync(fixture.A.Id, IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(fixture.A.IsActive);
        Assert.Equal(10_000_000m, fixture.B.MaxPrice);
        Assert.Null(fixture.C.MaxPrice);
        var audit = Assert.Single(fixture.Audits);
        Assert.Equal("commission.rate.retired", audit.Action);
        Assert.Equal(fixture.A.Id, audit.EntityId);
        Assert.NotNull(audit.NewValues);
        Assert.DoesNotContain("MinPrice", audit.NewValues, StringComparison.OrdinalIgnoreCase);
        fixture.Cache.Verify(cache => cache.InvalidateCommissionRatesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Transaction.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Retiring the open-ended top promotes the highest remaining active band in the same write.</summary>
    [Fact]
    public async Task RetireAsync_TopBand_PromotesHighestRemainingBand()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.RetireAsync(fixture.C.Id, IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(fixture.C.IsActive);
        Assert.Null(fixture.B.MaxPrice);
        Assert.False(fixture.B.MaxInclusive);
        Assert.Equal(1.75m, fixture.B.RatePercent);
        Assert.Equal(2, fixture.Audits.Count);
        var retired = Assert.Single(fixture.Audits, audit => audit.Action == "commission.rate.retired");
        Assert.Equal(fixture.C.Id, retired.EntityId);
        var promoted = Assert.Single(fixture.Audits, audit => audit.Action == "commission.rate.promoted");
        Assert.Equal(fixture.B.Id, promoted.EntityId);
        Assert.NotNull(promoted.OldValues);
        Assert.NotNull(promoted.NewValues);
        Assert.Contains("10000000", promoted.OldValues, StringComparison.Ordinal);
        Assert.Contains("\"MaxPrice\":null", promoted.NewValues, StringComparison.Ordinal);
        fixture.Cache.Verify(cache => cache.InvalidateCommissionRatesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Retiring the last active band would leave an empty set and is a 409.</summary>
    [Fact]
    public async Task RetireAsync_LastActiveBand_ReturnsConflict()
    {
        var fixture = Fixture.Create();
        fixture.Tiers.RemoveAll(band => band.Id != fixture.C.Id);

        var result = await fixture.Service.RetireAsync(fixture.C.Id, IfMatch, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(ErrorCodes.TierSetEmpty, result.Error.Code);
        Assert.True(fixture.C.IsActive);
        Assert.Empty(fixture.Audits);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.Transaction.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Reactivating a retired band re-validates the whole set and audits the change.</summary>
    [Fact]
    public async Task ReactivateAsync_RetiredBand_ReactivatesAndAudits()
    {
        var fixture = Fixture.Create();
        fixture.A.Retire();

        var result = await fixture.Service.ReactivateAsync(fixture.A.Id, IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(fixture.A.IsActive);
        Assert.NotNull(result.Value);
        Assert.Equal(0m, result.Value.MinPrice);
        Assert.True(result.Value.MinInclusive);
        var audit = Assert.Single(fixture.Audits);
        Assert.Equal("commission.rate.reactivated", audit.Action);
        Assert.Equal(fixture.A.Id, audit.EntityId);
        Assert.NotNull(audit.OldValues);
        Assert.NotNull(audit.NewValues);
        Assert.Contains("\"IsActive\":false", audit.OldValues, StringComparison.Ordinal);
        Assert.Contains("\"IsActive\":true", audit.NewValues, StringComparison.Ordinal);
        fixture.Cache.Verify(cache => cache.InvalidateCommissionRatesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Transaction.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Reactivating a band that would break the set is a 409 and leaves it retired.</summary>
    [Fact]
    public async Task ReactivateAsync_WouldBreakSet_ReturnsConflict()
    {
        var fixture = Fixture.Create();
        var duplicate = CommissionRate.Create(5_000_000m, true, 3m, DateTime.MinValue);
        duplicate.Retire();
        SetRowVersion(duplicate, RowVersion);
        fixture.Tiers.Add(duplicate);

        var result = await fixture.Service.ReactivateAsync(duplicate.Id, IfMatch, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(ErrorCodes.TierMaxNotAscending, result.Error.Code);
        Assert.False(duplicate.IsActive);
        Assert.Empty(fixture.Audits);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.Transaction.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Reactivating an already active band returns its DTO without writing.</summary>
    [Fact]
    public async Task ReactivateAsync_AlreadyActive_ReturnsDtoWithoutWriting()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.ReactivateAsync(fixture.A.Id, IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(0m, result.Value.MinPrice);
        Assert.Empty(fixture.Audits);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.Transaction.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Retired and future bands expose no derived lower bound.</summary>
    [Fact]
    public async Task GetAllAsync_RetiredBand_KeepsDerivedMin_FutureBand_HasNoDerivedMin()
    {
        var fixture = Fixture.Create();
        fixture.A.Retire();
        var future = CommissionRate.Create(15_000_000m, false, 1.25m, TimeProvider.System.GetUtcNow().UtcDateTime.AddDays(1));
        SetRowVersion(future, RowVersion);
        fixture.Tiers.Add(future);

        var result = await fixture.Service.GetAllAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var retired = Assert.Single(result.Value, dto => dto.Id == fixture.A.Id);
        Assert.Equal(0m, retired.MinPrice);
        Assert.True(retired.MinInclusive);
        Assert.False(retired.IsActive);
        var staged = Assert.Single(result.Value, dto => dto.Id == future.Id);
        Assert.Null(staged.MinPrice);
        Assert.Null(staged.MinInclusive);
        Assert.True(staged.IsActive);
        var firstActive = Assert.Single(result.Value, dto => dto.Id == fixture.B.Id);
        Assert.Equal(0m, firstActive.MinPrice);
        Assert.True(firstActive.MinInclusive);
        var openEnded = Assert.Single(result.Value, dto => dto.Id == fixture.C.Id);
        Assert.Equal(10_000_000m, openEnded.MinPrice);
        Assert.False(openEnded.MinInclusive);
    }

    private static CommissionRateRequest Request(decimal? maxPrice, bool maxInclusive, decimal ratePercent, DateTime? effectiveFrom = null)
        => new()
        {
            MaxPrice = maxPrice,
            MaxInclusive = maxInclusive,
            RatePercent = ratePercent,
            EffectiveFrom = effectiveFrom ?? default,
        };

    private static void SetRowVersion(CommissionRate rate, byte[] value)
        => typeof(CommissionRate).GetProperty(nameof(CommissionRate.RowVersion))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(rate, new object[] { value });

    /// <summary>Three-band set: A [0, 5M), B [5M, 10M], C (10M, open).</summary>
    private sealed class Fixture
    {
        private Fixture(CommissionRate a, CommissionRate b, CommissionRate c)
        {
            A = a;
            B = b;
            C = c;
            Unit = new Mock<IUnitOfWork>();
            Transaction = new Mock<ITransactionScope>();
            Repository = new Mock<ICommissionRateRepository>();
            Cache = new Mock<ICacheService>();
            CurrentUser = new Mock<ICurrentUserService>();
            Audits = new List<AuditEvent>();
            Tiers = new List<CommissionRate> { a, b, c };

            Repository.Setup(repo => repo.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Tiers.ToList());
            Repository.Setup(repo => repo.GetAllForUpdateAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Tiers.ToList());
            Repository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => Tiers.SingleOrDefault(tier => tier.Id == id));
            Repository.Setup(repo => repo.AddAsync(It.IsAny<CommissionRate>(), It.IsAny<CancellationToken>()))
                .Callback((CommissionRate rate, CancellationToken _) => Tiers.Add(rate))
                .Returns(Task.CompletedTask);
            Repository.Setup(repo => repo.AddAuditEventAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
                .Callback((AuditEvent audit, CancellationToken _) => Audits.Add(audit))
                .Returns(Task.CompletedTask);
            Unit.Setup(unit => unit.BeginTransactionAsync(It.IsAny<TransactionIsolationLevel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Transaction.Object);
            Unit.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success<int>(1));
            Transaction.Setup(scope => scope.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Transaction.Setup(scope => scope.DisposeAsync()).Returns(ValueTask.CompletedTask);

            Service = new CommissionRateAdminService(
                Repository.Object,
                CurrentUser.Object,
                TimeProvider.System,
                Cache.Object,
                Unit.Object,
                new CommissionRateRequestValidator());
        }

        public CommissionRate A { get; }

        public CommissionRate B { get; }

        public CommissionRate C { get; }

        public List<CommissionRate> Tiers { get; }

        public List<AuditEvent> Audits { get; }

        public Mock<IUnitOfWork> Unit { get; }

        public Mock<ITransactionScope> Transaction { get; }

        public Mock<ICommissionRateRepository> Repository { get; }

        public Mock<ICacheService> Cache { get; }

        public Mock<ICurrentUserService> CurrentUser { get; }

        public CommissionRateAdminService Service { get; }

        public static Fixture Create()
        {
            var a = CommissionRate.Create(5_000_000m, false, 2m, DateTime.MinValue);
            var b = CommissionRate.Create(10_000_000m, true, 1.75m, DateTime.MinValue);
            var c = CommissionRate.Create(null, false, 1.5m, DateTime.MinValue);
            foreach (var tier in new[] { a, b, c })
            {
                SetRowVersion(tier, RowVersion);
            }

            return new Fixture(a, b, c);
        }
    }
}
