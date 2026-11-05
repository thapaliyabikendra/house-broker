using HouseBroker.Application.Contracts.Listings;
using HouseBroker.Application.Contracts;
using HouseBroker.Application.Abstractions;
using HouseBroker.Application.Listings;
using HouseBroker.Domain;
using HouseBroker.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Listing service logging tests.</summary>
public sealed class ListingLoggingTests
{
    /// <summary>Create logs an information event that identifies the listing.</summary>
    [Fact]
    public async Task CreateAsync_LogsInformationWithListingId()
    {
        var logger = new RecordingLogger<ListingService>();
        var (service, _) = CreateService(Guid.NewGuid(), logger);

        var result = await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Code ?? "null");
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains(result.Value!.Id.ToString(), StringComparison.Ordinal));
    }

    /// <summary>An unauthenticated create logs a warning.</summary>
    [Fact]
    public async Task CreateAsync_WithoutUser_LogsWarning()
    {
        var logger = new RecordingLogger<ListingService>();
        var (service, _) = CreateService(null, logger);

        await service.CreateAsync(ValidCreateRequest(), CancellationToken.None);

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);
    }

    /// <summary>A stale If-Match update logs a warning.</summary>
    [Fact]
    public async Task UpdateAsync_StaleIfMatch_LogsWarning()
    {
        var brokerId = Guid.NewGuid();
        var logger = new RecordingLogger<ListingService>();
        var (service, repository) = CreateService(brokerId, logger);
        var listing = CreateListing(brokerId);
        repository.Setup(value => value.GetByIdAsync(listing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listing);

        var result = await service.UpdateAsync(listing.Id, ValidUpdateRequest(), "\"AAAA\"", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("stale", StringComparison.Ordinal));
    }

    /// <summary>Delete logs an information event that identifies the listing.</summary>
    [Fact]
    public async Task DeleteAsync_LogsInformationWithListingId()
    {
        var brokerId = Guid.NewGuid();
        var logger = new RecordingLogger<ListingService>();
        var (service, repository) = CreateService(brokerId, logger);
        var listing = CreateListing(brokerId);
        repository.Setup(value => value.GetByIdAsync(listing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listing);

        var result = await service.DeleteAsync(listing.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains(listing.Id.ToString(), StringComparison.Ordinal));
    }

    private static (ListingService Service, Mock<IListingRepository> Repository) CreateService(
        Guid? brokerId,
        RecordingLogger<ListingService> logger)
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        currentUser.SetupGet(user => user.UserId).Returns(brokerId);
        currentUser.SetupGet(user => user.FullName).Returns("Broker Name");
        currentUser.SetupGet(user => user.Email).Returns("broker@example.com");
        rates.Setup(provider => provider.GetActiveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CommissionRate.Create(null, false, 2m, DateTime.MinValue) });
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
            logger);

        return (service, repository);
    }

    private static CreateListingRequest ValidCreateRequest() => new()
    {
        Title = "Title",
        Description = "Description",
        PropertyTypeId = Guid.NewGuid(),
        Price = 100m,
        Province = "P",
        City = "C",
        Locality = "L",
        PostalCode = "12345",
        Bedrooms = 1,
        Bathrooms = 1,
        AreaSqFt = 50m,
    };

    private static UpdateListingRequest ValidUpdateRequest() => new()
    {
        Title = "Title",
        Description = "Description",
        PropertyTypeId = Guid.NewGuid(),
        Price = 100m,
        Province = "P",
        City = "C",
        Locality = "L",
        PostalCode = "12345",
        Bedrooms = 1,
        Bathrooms = 1,
        AreaSqFt = 50m,
    };

    private static Listing CreateListing(Guid brokerId) => Listing.Create(
        brokerId,
        "Title",
        "Description",
        Guid.NewGuid(),
        100m,
        "P",
        "C",
        "L",
        "12345",
        1,
        1,
        50m);
}
