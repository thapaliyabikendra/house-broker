using HouseBroker.Auth.Controllers;
using HouseBroker.Auth.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace HouseBroker.Auth.Tests;

/// <summary>Tests for the end-session endpoint's request handling.</summary>
public class EndSessionControllerTests
{
    private static EndSessionController CreateController(HttpContext httpContext, Mock<SignInManager<ApplicationUser>> signInManager)
    {
        var controller = new EndSessionController(signInManager.Object)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = httpContext },
        };
        return controller;
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManager()
    {
        var userManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

        return new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Mock.Of<IOptions<IdentityOptions>>(),
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            Mock.Of<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<ApplicationUser>>());
    }

    /// <summary>A logout request that OpenIddict did not parse is rejected before any session is touched.</summary>
    [Fact]
    public async Task Logout_without_openiddict_request_returns_bad_request_and_keeps_session()
    {
        var signInManager = CreateSignInManager();
        var controller = CreateController(new DefaultHttpContext(), signInManager);

        var result = await controller.LogoutAsync();

        Assert.IsType<BadRequestResult>(result);
        signInManager.Verify(manager => manager.SignOutAsync(), Times.Never);
    }
}
