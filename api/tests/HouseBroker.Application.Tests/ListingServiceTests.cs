using HouseBroker.Application.Contracts;
using HouseBroker.Application.Services;
using HouseBroker.Domain;
using Moq;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Listing service tests.</summary>
public sealed class ListingServiceTests
{
    /// <summary>Create requires authenticated broker.</summary>
    [Fact]
    public async Task CreateAsync_WithoutUser_ReturnsForbidden()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var createValidator = new CreateListingRequestValidator();
        var updateValidator = new UpdateListingRequestValidator();

        currentUser.SetupGet(user => user.UserId).Returns((Guid?)null);

        var service = new ListingService(
            repository.Object,
            queries.Object,
            rates.Object,
            currentUser.Object,
            unitOfWork.Object,
            TimeProvider.System,
            createValidator,
            updateValidator);

        var result = await service.CreateAsync(new CreateListingRequest
        {
            Title = "Title",
            PropertyTypeId = Guid.NewGuid(),
            Price = 100m,
            Province = "P",
            City = "C",
            Locality = "L",
            PostalCode = "12345",
        }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
    }

    /// <summary>Create stores commission for valid broker.</summary>
    [Fact]
    public async Task CreateAsync_ValidBroker_ComputesCommission()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var createValidator = new CreateListingRequestValidator();
        var updateValidator = new UpdateListingRequestValidator();

        var brokerId = Guid.NewGuid();
        currentUser.SetupGet(user => user.UserId).Returns(brokerId);
        rates.Setup(provider => provider.GetActiveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CommissionRate.Create(0m, true, null, false, 2m, true, DateTime.MinValue) });
        unitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var service = new ListingService(
            repository.Object,
            queries.Object,
            rates.Object,
            currentUser.Object,
            unitOfWork.Object,
            TimeProvider.System,
            createValidator,
            updateValidator);

        var result = await service.CreateAsync(new CreateListingRequest
        {
            Title = "Title",
            PropertyTypeId = Guid.NewGuid(),
            Price = 100m,
            Province = "P",
            City = "C",
            Locality = "L",
            PostalCode = "12345",
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2m, result.Value!.CommissionRatePercent);
    }
}
