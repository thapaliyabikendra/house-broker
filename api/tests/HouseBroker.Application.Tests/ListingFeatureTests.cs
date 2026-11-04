using HouseBroker.Application.Contracts;
using HouseBroker.Application.Services;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using HouseBroker.TestSupport;
using Moq;
using Xunit;

namespace HouseBroker.Application.Tests;

/// <summary>Listing feature tests.</summary>
public sealed class ListingFeatureTests
{
    /// <summary>Create rejects feature identifiers that are not in the lookup.</summary>
    [Fact]
    public async Task CreateAsync_UnknownFeature_ReturnsValidationFailed()
    {
        var unknownFeatureId = Guid.NewGuid();
        var (service, _, queries, _) = CreateService();
        queries.Setup(query => query.GetFeaturesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Feature>());

        var request = CreateRequest();
        request.FeatureIds.Add(unknownFeatureId);

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(ErrorCodes.ValidationFailed, result.Error.Code);
    }

    /// <summary>Create maps the feature code and name from the lookup.</summary>
    [Fact]
    public async Task CreateAsync_KnownFeature_MapsCodeAndName()
    {
        var feature = Feature.Create("POOL", "Swimming pool", 1);
        var (service, _, queries, _) = CreateService();
        queries.Setup(query => query.GetFeaturesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { feature });

        var request = CreateRequest();
        request.FeatureIds.Add(feature.Id);

        var result = await service.CreateAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var mapped = Assert.Single(result.Value!.Features);
        Assert.Equal(feature.Id, mapped.Id);
        Assert.Equal("POOL", mapped.Code);
        Assert.Equal("Swimming pool", mapped.Name);
    }

    /// <summary>Replacing features keeps unchanged rows, removes dropped rows, and adds new rows.</summary>
    [Fact]
    public void ReplaceFeatures_ReconcilesRows()
    {
        var listing = Listing.Create(
            Guid.NewGuid(),
            "Title",
            string.Empty,
            Guid.NewGuid(),
            100m,
            "P",
            "C",
            "L",
            "12345",
            1,
            1,
            100m);
        var keep = Guid.NewGuid();
        var remove = Guid.NewGuid();
        var add = Guid.NewGuid();

        listing.ReplaceFeatures(new[] { keep, remove });
        var keptRow = listing.Features.Single(feature => feature.FeatureId == keep);

        listing.ReplaceFeatures(new[] { keep, add });

        Assert.Equal(2, listing.Features.Count);
        Assert.Contains(listing.Features, feature => feature.FeatureId == keep);
        Assert.Contains(listing.Features, feature => feature.FeatureId == add);
        Assert.DoesNotContain(listing.Features, feature => feature.FeatureId == remove);
        Assert.Same(keptRow, listing.Features.Single(feature => feature.FeatureId == keep));
    }

    private static (ListingService Service, Mock<IListingRepository> Repository, Mock<IListingQueries> Queries, Mock<IUnitOfWork> UnitOfWork) CreateService()
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

        return (service, repository, queries, unitOfWork);
    }

    private static CreateListingRequest CreateRequest() => new()
    {
        Title = "Title",
        PropertyTypeId = Guid.NewGuid(),
        Price = 100m,
        Province = "P",
        City = "C",
        Locality = "L",
        PostalCode = "12345",
    };
}
