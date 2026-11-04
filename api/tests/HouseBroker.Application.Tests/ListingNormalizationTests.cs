using HouseBroker.Application.Contracts;
using HouseBroker.Application.Services;
using HouseBroker.Domain;
using HouseBroker.TestSupport;
using Moq;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Request text normalization tests.</summary>
public sealed class ListingNormalizationTests
{
    /// <summary>Create normalizes text to NFC and trims surrounding whitespace.</summary>
    [Fact]
    public async Task CreateAsync_NormalizesTextToNfcAndTrims()
    {
        var (service, repository, _, _, _) = CreateService();

        var result = await service.CreateAsync(new CreateListingRequest
        {
            Title = "  Cafe\u0301  ",
            Description = "  Near the park  ",
            PropertyTypeId = Guid.NewGuid(),
            Price = 100m,
            Province = " Bagmati ",
            City = " Kathmandu ",
            Locality = " Baneshwor ",
            PostalCode = " 44600 ",
        }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Caf\u00e9", result.Value!.Title);
        Assert.Equal("Near the park", result.Value.Description);
        Assert.Equal("Bagmati", result.Value.Province);
        Assert.Equal("Kathmandu", result.Value.City);
        Assert.Equal("Baneshwor", result.Value.Locality);
        Assert.Equal("44600", result.Value.PostalCode);
        repository.Verify(value => value.AddAsync(It.IsAny<Listing>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Search normalizes the location filter before querying.</summary>
    [Fact]
    public async Task SearchAsync_NormalizesLocation()
    {
        var (service, _, queries, _, _) = CreateService();
        queries.Setup(value => value.SearchAsync(It.IsAny<ListingSearchQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ListingSummaryDto>());

        await service.SearchAsync(new ListingSearchQuery { Location = "  Cafe\u0301  " }, CancellationToken.None);

        queries.Verify(
            value => value.SearchAsync(
                It.Is<ListingSearchQuery>(query => query.Location == "Caf\u00e9"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static (ListingService Service, Mock<IListingRepository> Repository, Mock<IListingQueries> Queries, Mock<ICommissionRateProvider> Rates, Mock<IUnitOfWork> UnitOfWork) CreateService()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUser.SetupGet(user => user.UserId).Returns(Guid.NewGuid());
        currentUser.SetupGet(user => user.FullName).Returns("Broker Name");
        currentUser.SetupGet(user => user.Email).Returns("broker@example.com");
        rates.Setup(provider => provider.GetActiveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CommissionRate.Create(0m, true, null, false, 2m, true, DateTime.MinValue) });
        unitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success<int>(1));

        var service = new ListingService(
            repository.Object,
            queries.Object,
            rates.Object,
            currentUser.Object,
            unitOfWork.Object,
            TimeProvider.System,
            new CreateListingRequestValidator(),
            new UpdateListingRequestValidator(),
            new RecordingLogger<ListingService>());

        return (service, repository, queries, rates, unitOfWork);
    }
}
