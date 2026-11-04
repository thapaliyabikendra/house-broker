using Microsoft.Extensions.Configuration;
using Moq;
using OpenIddict.Abstractions;
using Xunit;

namespace HouseBroker.Auth.Tests;

/// <summary>Client registration service tests.</summary>
public sealed class ClientRegistrationServiceTests
{
    /// <summary>Creates a client when not found.</summary>
    [Fact]
    public async Task RegisterAsync_MissingClient_Creates()
    {
        var manager = new Mock<IOpenIddictApplicationManager>();
        manager.Setup(item => item.FindByClientIdAsync("demo", It.IsAny<CancellationToken>())).ReturnsAsync((object?)null);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Client:Id"] = "demo",
                ["Client:Name"] = "Demo",
                ["Client:RedirectUris:0"] = "https://localhost/callback",
                ["Client:Scopes:0"] = "openid",
            })
            .Build();

        var service = new ClientRegistrationService(manager.Object, configuration);

        await service.RegisterAsync();

        manager.Verify(item => item.CreateAsync(It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>Updates a client when found.</summary>
    [Fact]
    public async Task RegisterAsync_ExistingClient_Updates()
    {
        var existing = new object();
        var manager = new Mock<IOpenIddictApplicationManager>();
        manager.Setup(item => item.FindByClientIdAsync("demo", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Client:Id"] = "demo",
            })
            .Build();

        var service = new ClientRegistrationService(manager.Object, configuration);

        await service.RegisterAsync();

        manager.Verify(item => item.UpdateAsync(existing, It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
