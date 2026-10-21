using System.Security.Claims;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Filters;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Validators;
using NUnit.Framework;

namespace CW.Tests.ExternalAuth.SSO;

/// <summary>
/// The rules a sign-in passes through: bypass token, email-domain allowlist, group overage,
/// role changes, mapping values and privileged roles.
/// </summary>
[TestFixture]
public class SignInRulesTests
{
    #region Bypass token

    [Test]
    public void Bypass_token_matches_only_the_same_value()
    {
        LoginActionsFilter.TokensMatch("s3cret-token", "s3cret-token").Should().BeTrue();
        LoginActionsFilter.TokensMatch("s3cret-token", "s3cret-tokeN").Should().BeFalse();
        LoginActionsFilter.TokensMatch("s3cret", "s3cret-token").Should().BeFalse();
        LoginActionsFilter.TokensMatch(string.Empty, "s3cret-token").Should().BeFalse();
    }

    #endregion

    #region Email-domain allowlist

    [TestCase("ann@example.com", "", true, TestName = "Empty allowlist accepts any domain")]
    [TestCase("ann@example.com", "example.com", true)]
    [TestCase("ann@EXAMPLE.COM", "example.com", true, TestName = "Domain match ignores case")]
    [TestCase("ann@other.com", "example.com", false)]
    [TestCase("ann@sub.example.com", "example.com", false, TestName = "Exact entry does not match a subdomain")]
    [TestCase("ann@sub.example.com", "*.example.com", true)]
    [TestCase("ann@example.com", "*.example.com", true, TestName = "Wildcard also matches the bare domain")]
    [TestCase("ann@badexample.com", "*.example.com", false, TestName = "Wildcard needs the dot")]
    [TestCase("ann@b.com", "a.com, b.com", true, TestName = "List entries are trimmed")]
    [TestCase("ann", "example.com", false, TestName = "No @ is refused")]
    [TestCase("ann@", "example.com", false, TestName = "Empty domain is refused")]
    public void Email_domain_allowlist(string email, string allowlist, bool expected)
        => SsoUserProvisioningService.IsEmailDomainAllowed(email, allowlist).Should().Be(expected);

    #endregion

    #region Group overage

    [Test]
    public void Group_overage_is_detected_for_both_protocols()
    {
        Principal((SSOAuthenticationDefaults.OidcGroupsOverageClaimType, "{\"groups\":\"src1\"}"))
            .Apply(SsoUserProvisioningService.HasGroupsOverage).Should().BeTrue();
        Principal((SSOAuthenticationDefaults.OidcHasGroupsClaimType, "true"))
            .Apply(SsoUserProvisioningService.HasGroupsOverage).Should().BeTrue();
        Principal((SSOAuthenticationDefaults.SamlGroupsOverageClaimType, "https://graph.microsoft.com/..."))
            .Apply(SsoUserProvisioningService.HasGroupsOverage).Should().BeTrue();
    }

    [Test]
    public void No_overage_for_an_ordinary_token()
    {
        Principal(("groups", "11111111-2222-3333-4444-555555555555"), (SSOAuthenticationDefaults.OidcHasGroupsClaimType, "false"),
                (SSOAuthenticationDefaults.OidcGroupsOverageClaimType, "{\"other\":\"src1\"}"))
            .Apply(SsoUserProvisioningService.HasGroupsOverage).Should().BeFalse();
    }

    #endregion

    #region Claim types as .NET's OIDC handler renames them

    [TestCase("roles", "roles", true, TestName = "Configured name matches as is")]
    [TestCase("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "roles", true, TestName = "roles also matches the renamed role claim (OIDC)")]
    [TestCase("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "role", true)]
    [TestCase("groups", "groups", true, TestName = "groups is not renamed and still matches")]
    [TestCase("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "groups", false, TestName = "groups does not pick up role claims")]
    [TestCase("roles", "", false, TestName = "An empty setting matches nothing")]
    [TestCase("roles", null, false)]
    public void Configured_claim_type(string claimType, string? configured, bool expected)
        => SsoUserProvisioningService.IsConfiguredClaimType(claimType, configured).Should().Be(expected);

    #endregion

    #region Role changes

    [Test]
    public void Grants_resolved_roles_and_removes_unresolved_owned_ones()
    {
        // Owned 1,2,3. Token resolves 1 and 3. Customer has 2 (owned, not resolved) and 9 (not owned).
        var (toAdd, toRemove) = SsoUserProvisioningService.ComputeRoleChanges(
            resolved: [1, 3], owned: [1, 2, 3], current: [2, 9]);

        toAdd.Should().BeEquivalentTo([1, 3]);
        toRemove.Should().BeEquivalentTo([2]);
    }

    [Test]
    public void Roles_outside_the_owned_set_are_never_touched()
    {
        // A resolved role the store does not own is not granted; a current role it does not own stays.
        var (toAdd, toRemove) = SsoUserProvisioningService.ComputeRoleChanges(
            resolved: [5], owned: [1], current: [7]);

        toAdd.Should().BeEmpty();
        toRemove.Should().BeEmpty();
    }

    [Test]
    public void Nothing_changes_when_the_customer_already_matches()
    {
        var (toAdd, toRemove) = SsoUserProvisioningService.ComputeRoleChanges(
            resolved: [1, 2], owned: [1, 2], current: [1, 2]);

        toAdd.Should().BeEmpty();
        toRemove.Should().BeEmpty();
    }

    #endregion

    #region Mapping values and privileged roles

    [TestCase("11111111-2222-3333-4444-555555555555", true, TestName = "Group object id")]
    [TestCase("Store.Manager", true, TestName = "App role value")]
    [TestCase("api://app/Role:Admin", true, TestName = "Colon and slash allowed")]
    [TestCase("has space", false)]
    [TestCase(".leading-dot", false)]
    [TestCase("", false)]
    public void Mapping_value_pattern(string value, bool expected)
        => Regex.IsMatch(value, SsoGroupRoleMappingModelValidator.IdpValuePattern).Should().Be(expected);

    [Test]
    public void Mapping_value_is_at_most_100_characters()
    {
        Regex.IsMatch(new string('a', 100), SsoGroupRoleMappingModelValidator.IdpValuePattern).Should().BeTrue();
        Regex.IsMatch(new string('a', 101), SsoGroupRoleMappingModelValidator.IdpValuePattern).Should().BeFalse();
    }

    [TestCase("Administrators", true)]
    [TestCase("administrators", true, TestName = "Privileged check ignores case")]
    [TestCase("Registered", false)]
    [TestCase(null, false)]
    public void Privileged_roles(string? systemName, bool expected)
        => SsoRoles.IsPrivileged(systemName).Should().Be(expected);

    #endregion

    #region Helpers

    private static ClaimsPrincipal Principal(params (string type, string value)[] claims)
        => new(new ClaimsIdentity(claims.Select(c => new Claim(c.type, c.value)), "test"));

    #endregion
}

internal static class PrincipalTestExtensions
{
    public static bool Apply(this ClaimsPrincipal principal, Func<ClaimsPrincipal, bool> check) => check(principal);
}
