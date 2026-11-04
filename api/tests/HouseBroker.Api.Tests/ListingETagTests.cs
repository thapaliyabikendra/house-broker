using HouseBroker.Application.Contracts.Listings;
using HouseBroker.Api.Controllers;
using HouseBroker.Application.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace HouseBroker.Api.Tests;

/// <summary>Listing detail ETag tests.</summary>
public sealed class ListingETagTests
{
    /// <summary>Public detail exposes the row version as a strong ETag.</summary>
    [Fact]
    public async Task PublicDetail_SetsETagFromRowVersion()
    {
        var version = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        var service = new Mock<IListingService>();
        service.Setup(listing => listing.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<ListingDetailDto>(new ListingDetailDto { Version = version }));
        var controller = new PublicListingsController(service.Object, Mock.Of<IImageStorage>())
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext() },
        };

        await controller.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal($"\"{version}\"", controller.Response.Headers.ETag.ToString());
    }

    /// <summary>Owner detail exposes the row version as a strong ETag.</summary>
    [Fact]
    public async Task OwnerDetail_SetsETagFromRowVersion()
    {
        var version = Convert.ToBase64String(new byte[] { 5, 6, 7, 8 });
        var service = new Mock<IListingService>();
        service.Setup(listing => listing.GetOwnerDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<OwnerListingDto>(new OwnerListingDto { Version = version }));
        var controller = new MyListingsController(service.Object, Mock.Of<IImageStorage>())
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext() },
        };

        await controller.GetMineById(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal($"\"{version}\"", controller.Response.Headers.ETag.ToString());
    }

    /// <summary>A missing version does not produce an empty ETag header.</summary>
    [Fact]
    public async Task PublicDetail_WithoutVersion_DoesNotSetETag()
    {
        var service = new Mock<IListingService>();
        service.Setup(listing => listing.GetDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<ListingDetailDto>(new ListingDetailDto()));
        var controller = new PublicListingsController(service.Object, Mock.Of<IImageStorage>())
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext() },
        };

        await controller.Get(Guid.NewGuid(), CancellationToken.None);

        Assert.True(string.IsNullOrEmpty(controller.Response.Headers.ETag));
    }

    /// <summary>The public detail is rate-limited with the search policy, since it returns contact details.</summary>
    [Fact]
    public void PublicDetail_IsRateLimitedWithSearchPolicy()
    {
        var method = typeof(PublicListingsController).GetMethod(nameof(PublicListingsController.Get))!;
        var attribute = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>()
            .Single();

        Assert.Equal(RateLimiting.RateLimitPolicies.Search, attribute.PolicyName);
    }

    private static DefaultHttpContext CreateHttpContext()
        => new() { RequestServices = new ServiceCollection().BuildServiceProvider() };
}
