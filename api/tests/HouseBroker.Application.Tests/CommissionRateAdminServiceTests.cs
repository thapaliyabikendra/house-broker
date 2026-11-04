using HouseBroker.Application.Contracts;
using HouseBroker.Application.Contracts.CommissionRates;
using HouseBroker.Application.CommissionRates;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using Moq;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Commission rate admin service update tests (tier boundary cascade and transactional checks).</summary>
public sealed class CommissionRateAdminServiceTests
{
    private static readonly byte[] RowVersion = [1, 2, 3, 4];
    private static readonly string IfMatch = Convert.ToBase64String(RowVersion);

    /// <summary>Raising a tier's max moves the adjacent successor's min to the same boundary.</summary>
    [Fact]
    public async Task UpdateAsync_RaisingMax_CascadesToSuccessorMin()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(0m, true, 6000000m, false, 2m), IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(6000000m, fixture.A.MaxPrice);
        Assert.False(fixture.A.MaxInclusive);
        Assert.Equal(6000000m, fixture.B.MinPrice);
        Assert.True(fixture.B.MinInclusive);
        Assert.Equal(10000000m, fixture.B.MaxPrice);
        Assert.True(fixture.B.MaxInclusive);
        Assert.Equal(1.75m, fixture.B.RatePercent);
    }

    /// <summary>Lowering a tier's max moves the adjacent successor's min down to the same boundary.</summary>
    [Fact]
    public async Task UpdateAsync_LoweringMax_CascadesToSuccessorMin()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(0m, true, 4000000m, false, 2m), IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4000000m, fixture.A.MaxPrice);
        Assert.Equal(4000000m, fixture.B.MinPrice);
        Assert.True(fixture.B.MinInclusive);
    }

    /// <summary>Flipping a tier's MaxInclusive flips the successor's MinInclusive.</summary>
    [Fact]
    public async Task UpdateAsync_ChangingMaxInclusive_FlipsSuccessorMinInclusive()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(0m, true, 5000000m, true, 2m), IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(fixture.A.MaxInclusive);
        Assert.Equal(5000000m, fixture.B.MinPrice);
        Assert.False(fixture.B.MinInclusive);
    }

    /// <summary>Lowering a tier's min moves the adjacent predecessor's max down to the same boundary.</summary>
    [Fact]
    public async Task UpdateAsync_LoweringMin_CascadesToPredecessorMax()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.UpdateAsync(fixture.B.Id, Request(4000000m, true, 10000000m, true, 1.75m), IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4000000m, fixture.B.MinPrice);
        Assert.Equal(4000000m, fixture.A.MaxPrice);
        Assert.False(fixture.A.MaxInclusive);
        Assert.Equal(0m, fixture.A.MinPrice);
        Assert.Equal(2m, fixture.A.RatePercent);
    }

    /// <summary>Each changed tier gets its own audit event with redacted old and new values.</summary>
    [Fact]
    public async Task UpdateAsync_Cascade_WritesAuditEventPerChangedTier()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(0m, true, 6000000m, false, 2m), IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var updates = fixture.Audits.Where(audit => audit.Action == "commission.rate.updated").ToList();
        Assert.Equal(2, updates.Count);
        Assert.Contains(updates, audit => audit.EntityId == fixture.A.Id);
        Assert.Contains(updates, audit => audit.EntityId == fixture.B.Id);
        Assert.All(updates, audit => Assert.NotNull(audit.NewValues));
        Assert.Single(updates, audit => audit.EntityId == fixture.A.Id);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        fixture.Transaction.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Updating only the rate of the top open-ended tier leaves neighbours untouched.</summary>
    [Fact]
    public async Task UpdateAsync_TopOpenEndedRateOnly_DoesNotCascade()
    {
        var fixture = Fixture.Create();
        var originalB = (fixture.B.MinPrice, fixture.B.MaxPrice, fixture.B.RatePercent);

        var result = await fixture.Service.UpdateAsync(fixture.C.Id, Request(10000000m, false, null, false, 1.25m), IfMatch, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1.25m, fixture.C.RatePercent);
        Assert.Equal(originalB, (fixture.B.MinPrice, fixture.B.MaxPrice, fixture.B.RatePercent));
        Assert.Single(fixture.Audits);
    }

    /// <summary>A cascade that would make the successor invalid fails with a field error and persists nothing.</summary>
    [Fact]
    public async Task UpdateAsync_CascadeProducesInvalidSet_FailsAndPersistsNothing()
    {
        var fixture = Fixture.Create();

        // B's max is 10,000,000 inclusive; raising A to 11,000,000 would invert B.
        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(0m, true, 11000000m, false, 2m), IfMatch, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.Error!.Code);
        Assert.Equal(ErrorKind.Validation, result.Error.Kind);
        Assert.Equal("MaxPrice", result.Error.Field);
        Assert.Equal(5000000m, fixture.A.MaxPrice);
        Assert.Equal(5000000m, fixture.B.MinPrice);
        Assert.True(fixture.B.MinInclusive);
        Assert.Empty(fixture.Audits);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        fixture.Transaction.Verify(scope => scope.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A stale If-Match is rejected with 412 semantics and nothing is written.</summary>
    [Fact]
    public async Task UpdateAsync_StaleIfMatch_FailsWithPreconditionFailed()
    {
        var fixture = Fixture.Create();
        var stale = Convert.ToBase64String([9, 9, 9]);

        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(0m, true, 6000000m, false, 2m), stale, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.PreconditionFailed, result.Error!.Code);
        Assert.Equal(5000000m, fixture.A.MaxPrice);
        Assert.Equal(5000000m, fixture.B.MinPrice);
        Assert.Empty(fixture.Audits);
        fixture.Unit.Verify(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }



    /// <summary>Violations on a cascaded neighbour are remapped to the edited field and keep their args.</summary>
    [Fact]
    public async Task UpdateAsync_CascadeInvalid_RemappedFieldErrorsKeepArgs()
    {
        var fixture = Fixture.Create();

        // Raising A's max to 11,000,000 (exclusive) moves B's min to 11,000,000 (inclusive), above B's max of 10,000,000.
        var result = await fixture.Service.UpdateAsync(fixture.A.Id, Request(0m, true, 11000000m, false, 2m), IfMatch, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.All(result.Error!.FieldErrors, error => Assert.Equal("MaxPrice", error.Field));
        var outOfOrder = Assert.Single(result.Error.FieldErrors, error => error.Code == ErrorCodes.TierMaxNotAboveMin);
        Assert.Equal(new object?[] { 11000000m, 10000000m }, outOfOrder.Args);
        var boundary = Assert.Single(result.Error.FieldErrors, error => error.Code == ErrorCodes.TierBoundaryMismatch);
        Assert.Equal(new object?[] { 11000000m, 10000000m }, boundary.Args);
    }

    private static CommissionRateRequest Request(decimal minPrice, bool minInclusive, decimal? maxPrice, bool maxInclusive, decimal ratePercent)
        => new()
        {
            MinPrice = minPrice,
            MinInclusive = minInclusive,
            MaxPrice = maxPrice,
            MaxInclusive = maxInclusive,
            RatePercent = ratePercent,
            IsActive = true,
        };

    private static void SetRowVersion(CommissionRate rate, byte[] value)
        => typeof(CommissionRate).GetProperty(nameof(CommissionRate.RowVersion))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(rate, new object[] { value });

    /// <summary>Three-tier set: A [0, 5M), B [5M, 10M], C (10M, open).</summary>
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
            Repository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => Tiers.SingleOrDefault(tier => tier.Id == id));
            Repository.Setup(repo => repo.AddAuditEventAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()))
                .Callback((AuditEvent audit, CancellationToken _) => Audits.Add(audit))
                .Returns(Task.CompletedTask);
            Unit.Setup(unit => unit.BeginTransactionAsync(It.IsAny<TransactionIsolationLevel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Transaction.Object);
            Unit.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result.Success<int>(1));
            Transaction.Setup(scope => scope.DisposeAsync()).Returns(ValueTask.CompletedTask);

            Service = new CommissionRateAdminService(
                Repository.Object,
                CurrentUser.Object,
                TimeProvider.System,
                Cache.Object,
                Unit.Object);
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
            var a = CommissionRate.Create(0m, true, 5000000m, false, 2m, true, DateTime.MinValue);
            var b = CommissionRate.Create(5000000m, true, 10000000m, true, 1.75m, true, DateTime.MinValue);
            var c = CommissionRate.Create(10000000m, false, null, false, 1.5m, true, DateTime.MinValue);
            foreach (var tier in new[] { a, b, c })
            {
                SetRowVersion(tier, RowVersion);
            }

            return new Fixture(a, b, c);
        }
    }
}
