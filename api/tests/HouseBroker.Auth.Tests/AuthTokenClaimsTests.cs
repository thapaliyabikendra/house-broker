using System.Security.Claims;
using HouseBroker.Domain.Common;
using OpenIddict.Abstractions;
using Xunit;

namespace HouseBroker.Auth.Tests;

/// <summary>Tests for the claims issued to user access tokens.</summary>
public class AuthTokenClaimsTests
{
    private static readonly Guid UserId = Guid.Parse("0196d1f0-0000-7000-8000-000000000001");
    private static readonly string[] BrokerRoles = [Roles.Broker];
    private static readonly string[] DefaultScopes = ["openid", "listings.read"];
    private static readonly string[] AdminRoles = [Roles.Admin];
    private static readonly string[] IdentityClaimTypes = [OpenIddictConstants.Claims.Name, OpenIddictConstants.Claims.Email];
    private static readonly string[] AccessOnly = [OpenIddictConstants.Destinations.AccessToken];
    private static readonly string[] SeveralScopes = ["openid", "listings.read", "commission.read"];

    private static ClaimsIdentity Build(
        IReadOnlyCollection<string>? roles = null,
        string? email = "broker@demo.com",
        IEnumerable<string>? scopes = null)
        => AuthTokenClaims.CreateUserIdentity(
            authenticationType: "test",
            userId: UserId,
            userName: "broker@demo.com",
            email: email,
            roles: roles ?? BrokerRoles,
            scopes: scopes ?? DefaultScopes);

    private static List<string> DestinationsOf(ClaimsIdentity identity, string type)
        => identity.FindAll(type).Single().GetDestinations().ToList();

    /// <summary>Subject is the user's id, sent in the access token.</summary>
    [Fact]
    public void Subject_is_user_id_and_is_sent_to_access_token()
    {
        var identity = Build();

        Assert.Equal(UserId.ToString(), identity.FindFirst(OpenIddictConstants.Claims.Subject)?.Value);
        Assert.Contains(OpenIddictConstants.Destinations.AccessToken, DestinationsOf(identity, OpenIddictConstants.Claims.Subject));
    }

    /// <summary>Name and email are identity-token claims and are also sent to the access token.</summary>
    [Fact]
    public void Name_and_email_are_sent_to_access_and_identity_tokens()
    {
        var identity = Build();

        foreach (var type in IdentityClaimTypes)
        {
            var destinations = DestinationsOf(identity, type);
            Assert.Contains(OpenIddictConstants.Destinations.AccessToken, destinations);
            Assert.Contains(OpenIddictConstants.Destinations.IdentityToken, destinations);
        }
    }

    /// <summary>Without an email address no email claim is issued.</summary>
    [Fact]
    public void Email_claim_is_omitted_when_user_has_no_email()
    {
        var identity = Build(email: null);

        Assert.Null(identity.FindFirst(OpenIddictConstants.Claims.Email));
    }

    /// <summary>Roles are sent to the access token; the API's permission checks depend on them.</summary>
    [Fact]
    public void Role_claims_are_sent_to_access_token()
    {
        var identity = Build(roles: AdminRoles);

        Assert.Equal(AccessOnly, DestinationsOf(identity, OpenIddictConstants.Claims.Role));
        Assert.Equal(Roles.Admin, identity.FindFirst(OpenIddictConstants.Claims.Role)?.Value);
    }

    /// <summary>Permission claims are derived from roles and sent to the access token.</summary>
    [Fact]
    public void Permission_claims_are_derived_from_roles_and_sent_to_access_token()
    {
        var identity = Build(roles: AdminRoles);

        var permissions = identity.FindAll("permission").Select(c => c.Value).ToList();
        Assert.Equal([AppPermissions.CommissionRatesManage], permissions);
        Assert.All(identity.FindAll("permission"), claim =>
            Assert.Equal(AccessOnly, claim.GetDestinations()));
    }

    /// <summary>Brokers receive broker permissions and not admin commission management.</summary>
    [Fact]
    public void Broker_permissions_exclude_commission_rate_management()
    {
        var identity = Build(roles: BrokerRoles);

        var permissions = identity.FindAll("permission").Select(c => c.Value).ToList();
        Assert.Contains(AppPermissions.ListingsCreate, permissions);
        Assert.DoesNotContain(AppPermissions.CommissionRatesManage, permissions);
    }

    /// <summary>Granted scopes are emitted as scp claims, not as OpenIddict's own scope claim.</summary>
    [Fact]
    public void Granted_scopes_are_emitted_as_scp_claims_sent_to_access_token()
    {
        var identity = Build(scopes: SeveralScopes);

        var scopes = identity.FindAll("scp").Select(c => c.Value).ToList();
        Assert.Equal(SeveralScopes, scopes);
        Assert.All(identity.FindAll("scp"), claim =>
            Assert.Equal(AccessOnly, claim.GetDestinations()));
    }

    /// <summary>Every issued claim has at least one destination, so none is silently dropped from a token.</summary>
    [Fact]
    public void Every_issued_claim_has_a_destination()
    {
        var identity = Build(roles: AdminRoles);

        // OpenIddict keeps its own "oi_" bookkeeping claims (such as the audience) internal; only ours must route.
        var issued = identity.Claims.Where(claim => !claim.Type.StartsWith("oi_", StringComparison.Ordinal));
        Assert.All(issued, claim => Assert.NotEmpty(claim.GetDestinations()));
    }

    /// <summary>Access tokens are scoped to the House Broker API audience.</summary>
    [Fact]
    public void Identity_targets_housebroker_api_audience()
    {
        var identity = Build();

        Assert.Equal(AuthTokenClaims.ApiAudience, Assert.Single(identity.GetAudiences()));
    }
}
