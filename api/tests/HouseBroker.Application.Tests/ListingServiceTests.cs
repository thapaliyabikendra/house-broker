using HouseBroker.Application.Contracts.Listings;
using HouseBroker.Application.Contracts;
using HouseBroker.Application.Abstractions;
using HouseBroker.Application.Listings;
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
        queries.Setup(q => q.GetFeaturesByIdsAsync(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Feature>());

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

    /// <summary>Every failing field is reported, not only the first one.</summary>
    [Fact]
    public async Task UpdateAsync_MultipleInvalidFields_ReportsEveryField()
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
        request.City = string.Empty;

        var result = await service.UpdateAsync(listing.Id, request, Convert.ToBase64String(listing.RowVersion), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        var fields = result.Error!.FieldErrors.Select(error => error.Field).ToList();
        Assert.Contains("Title", fields);
        Assert.Contains("City", fields);
        Assert.All(result.Error.FieldErrors, error => Assert.False(string.IsNullOrWhiteSpace(error.Code)));
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

    /// <summary>Detail includes broker contact for an anonymous caller (raw R13).</summary>
    [Fact]
    public async Task GetDetailAsync_Anonymous_IncludesBrokerContact()
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

        var result = await service.GetDetailAsync(listing.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.BrokerContact);
    }

    /// <summary>Reading the caller's own listings refreshes the broker profile from the token claims.</summary>
    [Fact]
    public async Task GetMineAsync_RefreshesBrokerProfileFromClaims()
    {
        var brokerId = Guid.NewGuid();
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        currentUser.Setup(user => user.UserId).Returns(brokerId);
        currentUser.Setup(user => user.FullName).Returns("Renamed Broker");
        currentUser.Setup(user => user.Email).Returns("renamed@example.com");
        currentUser.Setup(user => user.PhoneNumber).Returns("9800000099");
        queries.Setup(q => q.GetMineAsync(brokerId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Listing>());

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

        var result = await service.GetMineAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        queries.Verify(q => q.UpsertBrokerProfileAsync(
            It.Is<BrokerProfile>(profile => profile.BrokerId == brokerId
                && profile.FullName == "Renamed Broker"
                && profile.Email == "renamed@example.com"
                && profile.PhoneNumber == "9800000099"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A missing name claim does not block the broker's own listing read and writes no profile.</summary>
    [Fact]
    public async Task GetMineAsync_WithoutNameClaim_StillReturnsListings()
    {
        var brokerId = Guid.NewGuid();
        var repository = new Mock<IListingRepository>();
        var queries = new Mock<IListingQueries>();
        var rates = new Mock<ICommissionRateProvider>();
        var currentUser = new Mock<ICurrentUserService>();
        var unitOfWork = new Mock<IUnitOfWork>();
        currentUser.Setup(user => user.UserId).Returns(brokerId);
        queries.Setup(q => q.GetMineAsync(brokerId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Listing>());

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

        var result = await service.GetMineAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        queries.Verify(q => q.UpsertBrokerProfileAsync(It.IsAny<BrokerProfile>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>Detail exposes no commission fields on the public DTO.</summary>
    [Fact]
    public async Task GetDetailAsync_Anonymous_ExcludesCommissionFromPublicDto()
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

        var result = await service.GetDetailAsync(listing.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.IsNotType<OwnerListingDto>(result.Value);
        Assert.DoesNotContain(
            typeof(ListingDetailDto).GetProperties(),
            property => property.Name.StartsWith("Commission", StringComparison.Ordinal));
    }
}


