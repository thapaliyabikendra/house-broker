using HouseBroker.Application.Contracts.Listings;
using HouseBroker.Api.Controllers;
using HouseBroker.Application.Contracts;
using HouseBroker.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>
/// Controller tests for broker listing endpoints with a mocked <see cref="IListingService"/>.
/// Ownership and state rules live in the service (covered by <c>ListingServiceTests</c>); these tests
/// prove the controller forwards to the service, passes the <c>If-Match</c> precondition through, and maps
/// each expected outcome to the documented HTTP status.
/// </summary>
public sealed class BrokerListingsControllerTests
{
    /// <summary>A successful create returns 201 with the created owner DTO.</summary>
    [Fact]
    public async Task Create_Success_Returns201WithOwnerDto()
    {
        var created = new OwnerListingDto { Id = Guid.NewGuid() };
        var service = new Mock<IListingService>();
        service.Setup(listing => listing.CreateAsync(It.IsAny<CreateListingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(created));
        var controller = CreateController(service.Object);

        var response = await controller.Create(new CreateListingRequest(), CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(response);
        Assert.Same(created, createdResult.Value);
        service.Verify(listing => listing.CreateAsync(It.IsAny<CreateListingRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A non-owner attempting to update gets 403 and the service sees the caller's request unchanged.</summary>
    [Fact]
    public async Task Update_NonOwner_Returns403()
    {
        var service = new Mock<IListingService>();
        service.Setup(listing => listing.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateListingRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.Forbidden, ErrorKind.Forbidden)));
        var controller = CreateController(service.Object);

        var response = await controller.Update(Guid.NewGuid(), new UpdateListingRequest(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, StatusOf(response));
    }

    /// <summary>The <c>If-Match</c> header is passed to the service so the rowversion check can run there.</summary>
    [Fact]
    public async Task Update_ForwardsIfMatchHeaderToService()
    {
        const string etag = "\"AAECAwQ=\"";
        var service = new Mock<IListingService>();
        service.Setup(listing => listing.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateListingRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new OwnerListingDto()));
        var controller = CreateController(service.Object, headers: ("If-Match", etag));

        await controller.Update(Guid.NewGuid(), new UpdateListingRequest(), CancellationToken.None);

        service.Verify(listing => listing.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateListingRequest>(), etag, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A missing <c>If-Match</c> maps to 428 Precondition Required.</summary>
    [Fact]
    public async Task Update_MissingIfMatch_Returns428()
    {
        var service = new Mock<IListingService>();
        service.Setup(listing => listing.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateListingRequest>(), string.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.PreconditionRequired, ErrorKind.PreconditionRequired)));
        var controller = CreateController(service.Object);

        var response = await controller.Update(Guid.NewGuid(), new UpdateListingRequest(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status428PreconditionRequired, StatusOf(response));
    }

    /// <summary>A stale <c>If-Match</c> maps to 412 Precondition Failed.</summary>
    [Fact]
    public async Task Update_StaleIfMatch_Returns412()
    {
        var service = new Mock<IListingService>();
        service.Setup(listing => listing.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateListingRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<OwnerListingDto>(new ApplicationError(ErrorCodes.PreconditionFailed, ErrorKind.PreconditionFailed)));
        var controller = CreateController(service.Object, headers: ("If-Match", "\"stale\""));

        var response = await controller.Update(Guid.NewGuid(), new UpdateListingRequest(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status412PreconditionFailed, StatusOf(response));
    }

    /// <summary>Deleting a listing that is not the caller's (or does not exist) returns 404 without revealing which.</summary>
    [Fact]
    public async Task Delete_NotFoundOrNotOwned_Returns404()
    {
        var service = new Mock<IListingService>();
        service.Setup(listing => listing.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(new ApplicationError(ErrorCodes.ResourceNotFound, ErrorKind.NotFound)));
        var controller = CreateController(service.Object);

        var response = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(response));
    }

    /// <summary>A successful delete returns 204.</summary>
    [Fact]
    public async Task Delete_Success_Returns204()
    {
        var service = new Mock<IListingService>();
        service.Setup(listing => listing.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        var controller = CreateController(service.Object);

        var response = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(StatusCodes.Status204NoContent, StatusOf(response));
    }

    /// <summary>The "mine" list only returns what the service returns for the caller.</summary>
    [Fact]
    public async Task GetMine_ReturnsServiceResultUnchanged()
    {
        IReadOnlyList<OwnerListingDto> mine = [new OwnerListingDto { Id = Guid.NewGuid() }];
        var service = new Mock<IListingService>();
        service.Setup(listing => listing.GetMineAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(mine));
        var controller = CreateController(service.Object);

        var response = await controller.GetMine(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Same(mine, ok.Value);
    }

    private static BrokerListingsController CreateController(IListingService service, params (string Name, string Value)[] headers)
    {
        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() };
        foreach (var (name, value) in headers)
        {
            httpContext.Request.Headers[name] = value;
        }

        return new BrokerListingsController(service, Mock.Of<IImageStorage>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private static int? StatusOf(IActionResult response)
        => response switch
        {
            StatusCodeResult status => status.StatusCode,
            ObjectResult result => result.StatusCode,
            _ => null,
        };
}
