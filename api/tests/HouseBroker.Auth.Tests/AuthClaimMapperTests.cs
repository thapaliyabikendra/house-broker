using HouseBroker.Domain.Common;
using Xunit;

namespace HouseBroker.Auth.Tests;

/// <summary>Tests for Auth claim mapping.</summary>
public class AuthClaimMapperTests
{
    /// <summary>Brokers receive the documented permission set.</summary>
    [Fact]
    public void GetPermissions_Broker_ReturnsExpectedPermissions()
    {
        var permissions = AuthClaimMapper.GetPermissions(new List<string> { Roles.Broker });

        Assert.Contains(AppPermissions.ListingsView, permissions);
        Assert.Contains(AppPermissions.ListingsContactView, permissions);
        Assert.Contains(AppPermissions.ListingsCreate, permissions);
        Assert.Contains(AppPermissions.ListingsUpdate, permissions);
        Assert.Contains(AppPermissions.ListingsDelete, permissions);
        Assert.Contains(AppPermissions.ListingsImagesManage, permissions);
        Assert.Contains(AppPermissions.CommissionView, permissions);
        Assert.DoesNotContain(AppPermissions.CommissionRatesManage, permissions);
    }

    /// <summary>Seekers receive only listing view permissions.</summary>
    [Fact]
    public void GetPermissions_Seeker_ReturnsExpectedPermissions()
    {
        var permissions = AuthClaimMapper.GetPermissions(new List<string> { Roles.Seeker });

        Assert.Contains(AppPermissions.ListingsView, permissions);
        Assert.Contains(AppPermissions.ListingsContactView, permissions);
        Assert.DoesNotContain(AppPermissions.ListingsCreate, permissions);
        Assert.DoesNotContain(AppPermissions.CommissionView, permissions);
    }

    /// <summary>Admins receive commission management permission only.</summary>
    [Fact]
    public void GetPermissions_Admin_ReturnsExpectedPermissions()
    {
        var permissions = AuthClaimMapper.GetPermissions(new List<string> { Roles.Admin });

        Assert.Contains(AppPermissions.CommissionRatesManage, permissions);
        Assert.DoesNotContain(AppPermissions.ListingsView, permissions);
    }

    /// <summary>Unknown roles yield no permissions.</summary>
    [Fact]
    public void GetPermissions_UnknownRole_ReturnsEmpty()
    {
        var permissions = AuthClaimMapper.GetPermissions(new List<string> { "Unknown" });
        Assert.Empty(permissions);
    }

    /// <summary>Multiple roles union their permissions.</summary>
    [Fact]
    public void GetPermissions_MultipleRoles_UnionsPermissions()
    {
        var permissions = AuthClaimMapper.GetPermissions(new List<string> { Roles.Broker, Roles.Admin });

        Assert.Contains(AppPermissions.ListingsCreate, permissions);
        Assert.Contains(AppPermissions.CommissionRatesManage, permissions);
    }

    /// <summary>Identity claims target both tokens.</summary>
    [Theory]
    [InlineData("name")]
    [InlineData("email")]
    [InlineData("phone_number")]
    public void IsIdentityClaim_KnownIdentityClaim_ReturnsTrue(string claimType)
    {
        Assert.True(AuthClaimMapper.IsIdentityClaim(claimType));
    }

    /// <summary>Other claims are access-token only.</summary>
    [Theory]
    [InlineData("role")]
    [InlineData("permission")]
    [InlineData("scp")]
    public void IsIdentityClaim_OtherClaim_ReturnsFalse(string claimType)
    {
        Assert.False(AuthClaimMapper.IsIdentityClaim(claimType));
    }

    /// <summary>Registration roles are limited to Seeker and Broker.</summary>
    [Fact]
    public void RegistrationRoles_ContainsOnlyAllowedRoles()
    {
        Assert.Equal(2, AuthClaimMapper.RegistrationRoles.Length);
        Assert.Contains(Roles.Seeker, AuthClaimMapper.RegistrationRoles);
        Assert.Contains(Roles.Broker, AuthClaimMapper.RegistrationRoles);
        Assert.DoesNotContain(Roles.Admin, AuthClaimMapper.RegistrationRoles);
    }
}
