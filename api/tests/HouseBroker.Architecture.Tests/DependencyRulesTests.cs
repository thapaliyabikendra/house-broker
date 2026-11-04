using HouseBroker.Application.Contracts.Listings;
using HouseBroker.Domain;
using HouseBroker.Domain.Common;
using Xunit;

namespace HouseBroker.Architecture.Tests;

/// <summary>Dependency direction rules enforced for the layer projects.</summary>
public sealed class DependencyRulesTests
{
    private static string[] ReferencedAssemblyNames(Type marker)
        => marker.Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name ?? string.Empty).ToArray();

    /// <summary>Ensures the shared kernel has no project dependencies.</summary>
    [Fact]
    public void DomainShared_DoesNotReference_HouseBrokerProjects()
    {
        Assert.DoesNotContain(ReferencedAssemblyNames(typeof(ErrorCodes)), name => name.StartsWith("HouseBroker.", StringComparison.Ordinal));
    }

    /// <summary>Ensures domain does not reference upper product layers.</summary>
    [Fact]
    public void Domain_DoesNotReference_UpperLayers()
    {
        var references = ReferencedAssemblyNames(typeof(Listing));
        Assert.DoesNotContain(references, name => name == "HouseBroker.Application" || name == "HouseBroker.Application.Contracts" || name == "HouseBroker.Infrastructure");
    }

    /// <summary>Ensures contracts do not reference domain implementation or infrastructure.</summary>
    [Fact]
    public void ApplicationContracts_DoesNotReference_UpperLayers()
    {
        var references = ReferencedAssemblyNames(typeof(ListingSearchQuery));
        Assert.DoesNotContain(references, name => name == "HouseBroker.Domain" || name == "HouseBroker.Application" || name == "HouseBroker.Infrastructure");
    }

    /// <summary>Ensures application does not reference infrastructure.</summary>
    [Fact]
    public void Application_DoesNotReference_Infrastructure()
    {
        var references = ReferencedAssemblyNames(typeof(HouseBroker.Application.Listings.ListingService));
        Assert.DoesNotContain("HouseBroker.Infrastructure", references);
    }

    /// <summary>Ensures infrastructure references application.</summary>
    [Fact]
    public void Infrastructure_ReferencesApplication()
    {
        var references = ReferencedAssemblyNames(typeof(HouseBroker.Infrastructure.CachedListingQueries));
        Assert.Contains("HouseBroker.Application", references);
    }

    /// <summary>Ensures auth does not reference product domain or application layers.</summary>
    [Fact]
    public void Auth_DoesNotReference_DomainApplicationOrInfrastructure()
    {
        var references = ReferencedAssemblyNames(typeof(HouseBroker.Auth.AuthClaimMapper));
        Assert.DoesNotContain(references, name => name == "HouseBroker.Domain" || name == "HouseBroker.Application" || name == "HouseBroker.Application.Contracts" || name == "HouseBroker.Infrastructure");
    }

    /// <summary>Ensures service defaults does not reference product layers.</summary>
    [Fact]
    public void ServiceDefaults_DoesNotReference_ProductLayers()
    {
        var references = ReferencedAssemblyNames(typeof(HouseBroker.ServiceDefaults.ServiceDefaultsExtensions));
        Assert.DoesNotContain(references, name => name == "HouseBroker.Domain" || name == "HouseBroker.Application" || name == "HouseBroker.Application.Contracts" || name == "HouseBroker.Infrastructure" || name == "HouseBroker.Api" || name == "HouseBroker.Auth");
    }
}
