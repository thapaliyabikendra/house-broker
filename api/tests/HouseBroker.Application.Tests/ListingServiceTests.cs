using HouseBroker.Application.Contracts;
using HouseBroker.Application.Services;
using HouseBroker.Domain.Common;
using HouseBroker.Domain;
using HouseBroker.TestSupport;
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
            updateValidator,
            new RecordingLogger<ListingService>());

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
            createValidator,
            updateValidator,
            new RecordingLogger<ListingService>());

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

    /// <summary>Update recalculates commission.</summary>
    [Fact]
    public async Task UpdateAsync_ValidBroker_RecalculatesCommission()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var createValidator = new CreateListingRequestValidator();
        var updateValidator = new UpdateListingRequestValidator();

        var brokerId = Guid.NewGuid();
        var listing = ListingBuilder.Create(brokerId, price: 100m);
        typeof(Listing).GetProperty("RowVersion")!.SetValue(listing, new byte[] { 1, 2, 3 });
        currentUser.SetupGet(user => user.UserId).Returns(brokerId);
        currentUser.SetupGet(user => user.FullName).Returns("Broker Name");
        currentUser.SetupGet(user => user.Email).Returns("broker@example.com");
        repository.Setup(repo => repo.GetByIdAsync(listing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listing);
        rates.Setup(provider => provider.GetActiveAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CommissionRate.Create(0m, true, null, false, 3m, true, DateTime.MinValue) });
        unitOfWork.Setup(unit => unit.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success<int>(1));
        queries.Setup(q => q.GetFeaturesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Feature>());

        var service = new ListingService(
            repository.Object,
            queries.Object,
            rates.Object,
            currentUser.Object,
            unitOfWork.Object,
            TimeProvider.System,
            createValidator,
            updateValidator,
            new RecordingLogger<ListingService>());

        var result = await service.UpdateAsync(listing.Id, ListingRequestBuilder.CreateValidUpdateRequest(price: 200m), Convert.ToBase64String(listing.RowVersion), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(3m, result.Value!.CommissionRatePercent);
    }

    /// <summary>Update non-owner is forbidden.</summary>
    [Fact]
    public async Task UpdateAsync_NonOwner_ReturnsForbidden()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var owner = Guid.NewGuid();
        var caller = Guid.NewGuid();
        var listing = ListingBuilder.Create(owner);
        currentUser.SetupGet(user => user.UserId).Returns(caller);
        currentUser.SetupGet(user => user.FullName).Returns("Broker Name");
        currentUser.SetupGet(user => user.Email).Returns("broker@example.com");
        repository.Setup(repo => repo.GetByIdAsync(listing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listing);

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

        var result = await service.UpdateAsync(listing.Id, ListingRequestBuilder.CreateValidUpdateRequest(), Convert.ToBase64String(listing.RowVersion), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Forbidden, result.Error!.Kind);
    }

    /// <summary>Update not found.</summary>
    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsNotFound()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var brokerId = Guid.NewGuid();
        var id = Guid.NewGuid();
        currentUser.SetupGet(user => user.UserId).Returns(brokerId);
        currentUser.SetupGet(user => user.FullName).Returns("Broker Name");
        currentUser.SetupGet(user => user.Email).Returns("broker@example.com");
        repository.Setup(repo => repo.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Listing?)null);

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

        var result = await service.UpdateAsync(id, ListingRequestBuilder.CreateValidUpdateRequest(), "abc", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
    }

    /// <summary>Update validation failure.</summary>
    [Fact]
    public async Task UpdateAsync_InvalidRequest_ReturnsValidation()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var brokerId = Guid.NewGuid();
        var listing = ListingBuilder.Create(brokerId);
        currentUser.SetupGet(user => user.UserId).Returns(brokerId);
        currentUser.SetupGet(user => user.FullName).Returns("Broker Name");
        currentUser.SetupGet(user => user.Email).Returns("broker@example.com");
        repository.Setup(repo => repo.GetByIdAsync(listing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listing);

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

        var request = ListingRequestBuilder.CreateValidUpdateRequest();
        request.Title = string.Empty;

        var result = await service.UpdateAsync(listing.Id, request, Convert.ToBase64String(listing.RowVersion), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
    }

    /// <summary>Update missing If-Match.</summary>
    [Fact]
    public async Task UpdateAsync_MissingIfMatch_ReturnsPreconditionRequired()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var brokerId = Guid.NewGuid();
        var listing = ListingBuilder.Create(brokerId);
        currentUser.SetupGet(user => user.UserId).Returns(brokerId);
        currentUser.SetupGet(user => user.FullName).Returns("Broker Name");
        currentUser.SetupGet(user => user.Email).Returns("broker@example.com");
        repository.Setup(repo => repo.GetByIdAsync(listing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listing);

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

        var result = await service.UpdateAsync(listing.Id, ListingRequestBuilder.CreateValidUpdateRequest(), null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.PreconditionRequired, result.Error!.Kind);
    }

    /// <summary>Update stale If-Match.</summary>
    [Fact]
    public async Task UpdateAsync_StaleIfMatch_ReturnsPreconditionFailed()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var brokerId = Guid.NewGuid();
        var listing = ListingBuilder.Create(brokerId);
        currentUser.SetupGet(user => user.UserId).Returns(brokerId);
        currentUser.SetupGet(user => user.FullName).Returns("Broker Name");
        currentUser.SetupGet(user => user.Email).Returns("broker@example.com");
        repository.Setup(repo => repo.GetByIdAsync(listing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listing);

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

        var result = await service.UpdateAsync(listing.Id, ListingRequestBuilder.CreateValidUpdateRequest(), Convert.ToBase64String(Guid.NewGuid().ToByteArray()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.PreconditionFailed, result.Error!.Kind);
    }

    /// <summary>Search validates null.</summary>
    [Fact]
    public async Task SearchAsync_Null_ReturnsRequired()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();

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

        var result = await service.SearchAsync(null!, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(ErrorCodes.Required, result.Error.Code);
    }

    /// <summary>Search validates price range.</summary>
    [Fact]
    public async Task SearchAsync_MinGreaterThanMax_ReturnsOutOfRange()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();

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

        var query = new ListingSearchQuery { MinPrice = 200m, MaxPrice = 100m };
        var result = await service.SearchAsync(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(ErrorCodes.OutOfRange, result.Error.Code);
    }

    /// <summary>Detail hides contact without scope.</summary>
    [Fact]
    public async Task GetDetailAsync_WithoutContact_HidesBrokerContact()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var listing = ListingBuilder.Create();
        listing.Publish();
        queries.Setup(q => q.GetPublicAsync(listing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listing);

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

        var result = await service.GetDetailAsync(listing.Id, false, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.BrokerContact);
    }

    /// <summary>Detail includes contact with scope.</summary>
    [Fact]
    public async Task GetDetailAsync_WithContact_IncludesBrokerContact()
    {
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();

        var listing = ListingBuilder.Create();
        listing.Publish();
        queries.Setup(q => q.GetPublicAsync(listing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(listing);
        queries.Setup(q => q.GetBrokerProfileAsync(listing.BrokerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(HouseBroker.Domain.BrokerProfile.Create(listing.BrokerId, "Broker", "broker@example.com", "123"));

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

        var result = await service.GetDetailAsync(listing.Id, true, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.BrokerContact);
    }
}


