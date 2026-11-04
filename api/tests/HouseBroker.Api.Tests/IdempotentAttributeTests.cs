using HouseBroker.Api.Filters;
using HouseBroker.Application.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>
/// Tests for the <see cref="IdempotentAttribute"/> action filter with mocked application services.
/// ASP.NET action filters receive action exceptions in <see cref="ActionExecutedContext.Exception"/>
/// rather than as a thrown exception from <c>next()</c>, so these tests drive that shape directly.
/// </summary>
public sealed class IdempotentAttributeTests
{
    private const string ValidKey = "valid-key-0123456789";
    private const string Scope = "user:00000000-0000-0000-0000-000000000001";

    private sealed class FilterHarness
    {
        public FilterHarness()
        {
            Idempotency.Setup(service => service.ComputeRequestHash(It.IsAny<string>())).Returns("hash");
            Idempotency.Setup(service => service.BeginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IdempotencyResult(IdempotencyStatus.New, null, null));
            UnitOfWork.Setup(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<TransactionIsolationLevel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Transaction.Object);
            CurrentUser.Setup(user => user.UserId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));

            var services = new ServiceCollection();
            services.AddSingleton(Idempotency.Object);
            services.AddSingleton(UnitOfWork.Object);
            services.AddSingleton(CurrentUser.Object);
            services.AddSingleton<ILogger<IdempotentAttribute>>(NullLogger<IdempotentAttribute>.Instance);
            HttpContext.RequestServices = services.BuildServiceProvider();
            HttpContext.Request.Headers["Idempotency-Key"] = ValidKey;
        }

        public Mock<IIdempotencyService> Idempotency { get; } = new(MockBehavior.Loose);

        public Mock<IUnitOfWork> UnitOfWork { get; } = new(MockBehavior.Loose);

        public Mock<ITransactionScope> Transaction { get; } = new(MockBehavior.Loose);

        public Mock<ICurrentUserService> CurrentUser { get; } = new(MockBehavior.Loose);

        public DefaultHttpContext HttpContext { get; } = new();

        public ActionExecutingContext CreateExecutingContext()
        {
            var actionContext = new ActionContext(HttpContext, new RouteData(), new ActionDescriptor());
            var arguments = new Dictionary<string, object?> { ["request"] = new { Name = "Sample" } };
            return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), arguments, new object());
        }
    }

    /// <summary>Runs the filter with an action that either returns <paramref name="result"/> or fails with <paramref name="exception"/>.</summary>
    private static async Task<(ActionExecutingContext Executing, ActionExecutedContext Executed)> RunAsync(FilterHarness harness, IActionResult? result, Exception? exception = null)
    {
        var executing = harness.CreateExecutingContext();
        var executed = new ActionExecutedContext(executing, new List<IFilterMetadata>(), new object())
        {
            Result = result,
            Exception = exception,
        };

        ActionExecutionDelegate next = () => Task.FromResult(executed);
        await new IdempotentAttribute().OnActionExecutionAsync(executing, next);
        return (executing, executed);
    }

    /// <summary>An action exception rolls back the transaction, releases the key and stays unhandled for the framework to rethrow.</summary>
    [Fact]
    public async Task ActionException_RollsBackReleasesKeyAndLeavesExceptionUnhandled()
    {
        var failure = new InvalidOperationException("action failed");
        var harness = new FilterHarness();

        var (_, executed) = await RunAsync(harness, result: null, exception: failure);

        Assert.Same(failure, executed.Exception);
        Assert.False(executed.ExceptionHandled);
        Assert.Null(executed.Result);
        harness.Transaction.Verify(transaction => transaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        harness.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        harness.Idempotency.Verify(service => service.ReleaseAsync(ValidKey, Scope, It.IsAny<CancellationToken>()), Times.Once);
        harness.Idempotency.Verify(service => service.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>An action that produces no result releases the key and rolls back instead of committing.</summary>
    [Fact]
    public async Task NoResult_RollsBackAndReleasesKey()
    {
        var harness = new FilterHarness();

        var (_, executed) = await RunAsync(harness, result: null);

        Assert.Null(executed.Result);
        Assert.Null(executed.Exception);
        harness.Transaction.Verify(transaction => transaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        harness.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        harness.Idempotency.Verify(service => service.ReleaseAsync(ValidKey, Scope, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A 2xx result stores the response under the key and commits the business change with it.</summary>
    [Fact]
    public async Task Created_StoresResponseAndCommits()
    {
        var harness = new FilterHarness();

        var (_, executed) = await RunAsync(harness, new CreatedResult("/api/v1/admin/commission-rates/1", new { Id = 1 }));

        Assert.IsType<CreatedResult>(executed.Result);
        harness.Idempotency.Verify(service => service.CompleteAsync(ValidKey, Scope, StatusCodes.Status201Created, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        harness.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        harness.Transaction.Verify(transaction => transaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
        harness.Idempotency.Verify(service => service.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A client error result releases the key so the client can retry with the same key.</summary>
    [Fact]
    public async Task ClientError_ReleasesKeyAndRollsBack()
    {
        var harness = new FilterHarness();

        await RunAsync(harness, new ObjectResult(new { }) { StatusCode = StatusCodes.Status400BadRequest });

        harness.Transaction.Verify(transaction => transaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        harness.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        harness.Idempotency.Verify(service => service.ReleaseAsync(ValidKey, Scope, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An in-flight duplicate returns 409 with a Retry-After header so the client knows when to retry.</summary>
    [Fact]
    public async Task InProgress_Returns409WithRetryAfter()
    {
        var harness = new FilterHarness();
        harness.Idempotency.Setup(service => service.BeginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyResult(IdempotencyStatus.InProgress, null, null));

        var (executing, _) = await RunAsync(harness, result: null);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsType<ObjectResult>(executing.Result).StatusCode);
        Assert.Equal("1", harness.HttpContext.Response.Headers.RetryAfter.ToString());
    }

    /// <summary>A failing release is logged and does not stop the rollback.</summary>
    [Fact]
    public async Task ReleaseFailure_StillRollsBack()
    {
        var harness = new FilterHarness();
        harness.Idempotency.Setup(service => service.ReleaseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var (_, executed) = await RunAsync(harness, new ObjectResult(new { }) { StatusCode = StatusCodes.Status400BadRequest });

        Assert.IsType<ObjectResult>(executed.Result);
        harness.Transaction.Verify(transaction => transaction.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>An invalid key is rejected with 400 (validation), before any transaction starts.</summary>
    [Fact]
    public async Task InvalidKey_Returns400BeforeTransaction()
    {
        var harness = new FilterHarness();
        harness.HttpContext.Request.Headers["Idempotency-Key"] = "too-short";

        var (executing, _) = await RunAsync(harness, new CreatedResult("/unused", new { }));

        var problem = Assert.IsType<ObjectResult>(executing.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        harness.UnitOfWork.Verify(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<TransactionIsolationLevel>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The same key with a different body returns 422.</summary>
    [Fact]
    public async Task BodyMismatch_Returns422()
    {
        var harness = new FilterHarness();
        harness.Idempotency.Setup(service => service.BeginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyResult(IdempotencyStatus.Conflict, null, null));

        var (executing, _) = await RunAsync(harness, new CreatedResult("/unused", new { }));

        var problem = Assert.IsType<ObjectResult>(executing.Result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, problem.StatusCode);
    }

    /// <summary>A duplicate that is still in flight returns 409.</summary>
    [Fact]
    public async Task InFlightDuplicate_Returns409()
    {
        var harness = new FilterHarness();
        harness.Idempotency.Setup(service => service.BeginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyResult(IdempotencyStatus.InProgress, null, null));

        var (executing, _) = await RunAsync(harness, new CreatedResult("/unused", new { }));

        var problem = Assert.IsType<ObjectResult>(executing.Result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
    }

    /// <summary>A completed key replays the stored response with the replay header and does not run the action.</summary>
    [Fact]
    public async Task Replay_ReturnsStoredResponseWithHeader()
    {
        var harness = new FilterHarness();
        harness.Idempotency.Setup(service => service.BeginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IdempotencyResult(IdempotencyStatus.Replay, StatusCodes.Status201Created, "{\"id\":1}"));

        var (executing, _) = await RunAsync(harness, new CreatedResult("/unused", new { }));

        var replay = Assert.IsType<ContentResult>(executing.Result);
        Assert.Equal(StatusCodes.Status201Created, replay.StatusCode);
        Assert.Equal("{\"id\":1}", replay.Content);
        Assert.Equal("true", harness.HttpContext.Response.Headers["Idempotent-Replayed"].ToString());
        harness.Transaction.Verify(transaction => transaction.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
