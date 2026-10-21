using AwesomeAssertions;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using NUnit.Framework;
using static Nop.Plugin.ExternalAuth.SSO.Services.SsoMonitoringService;

namespace CW.Tests.ExternalAuth.SSO;

/// <summary>
/// The rules behind System → Warnings and the daily check: how close an expiry is, and how a
/// metadata comparison is described to the admin.
/// </summary>
[TestFixture]
public class MonitoringTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

    #region Expiry

    [Test]
    public void Past_expiry_is_expired_with_no_days_left()
        => GetExpiryState(Now.AddMinutes(-1), Now, 30).Should().Be((ExpiryState.Expired, 0));

    [Test]
    public void Expiry_at_this_moment_counts_as_expired()
        => GetExpiryState(Now, Now, 30).state.Should().Be(ExpiryState.Expired);

    [Test]
    public void Inside_the_window_is_expiring_with_whole_days_left()
        => GetExpiryState(Now.AddDays(10).AddHours(5), Now, 30).Should().Be((ExpiryState.Expiring, 10));

    [Test]
    public void Less_than_a_day_left_is_expiring_with_zero_days()
        => GetExpiryState(Now.AddHours(3), Now, 30).Should().Be((ExpiryState.Expiring, 0));

    [Test]
    public void Exactly_the_window_away_is_not_yet_reported()
        => GetExpiryState(Now.AddDays(30), Now, 30).state.Should().Be(ExpiryState.Ok);

    [Test]
    public void One_day_inside_the_window_is_reported()
        => GetExpiryState(Now.AddDays(29).AddHours(23), Now, 30).state.Should().Be(ExpiryState.Expiring);

    #endregion

    #region Admin lockout (D10)

    [TestCase(true, SsoProtocol.OpenIdConnect, true, false, true, TestName = "Redirect on, no bypass token: locked out")]
    [TestCase(true, SsoProtocol.Saml2, true, false, true)]
    [TestCase(true, SsoProtocol.OpenIdConnect, true, true, false, TestName = "A bypass token keeps the way in")]
    [TestCase(true, SsoProtocol.OpenIdConnect, false, false, false, TestName = "Redirect off: nop's form stays")]
    [TestCase(false, SsoProtocol.OpenIdConnect, true, false, false, TestName = "SSO disabled: no redirect")]
    [TestCase(true, SsoProtocol.Off, true, false, false, TestName = "Protocol off: no redirect")]
    public void Admin_lockout(bool enabled, SsoProtocol protocol, bool redirect, bool hasToken, bool expected)
        => LocksAdminsOut(enabled, protocol, redirect, hasToken).Should().Be(expected);

    #endregion

    #region Metadata differences

    [Test]
    public void Matching_metadata_has_no_differences()
        => SsoTestConnectionService.DescribeDifferences(Handshake()).Should().BeEmpty();

    [Test]
    public void No_metadata_url_is_not_a_difference()
        => SsoTestConnectionService.DescribeDifferences(new HandshakeResult { SkippedReason = "IdP Metadata URL is not configured." })
            .Should().BeEmpty();

    [Test]
    public void Unreadable_metadata_says_why()
        => SsoTestConnectionService.DescribeDifferences(new HandshakeResult { MetadataFetched = false, Detail = "Metadata fetch failed: 404" })
            .Should().Be("the metadata could not be read (Metadata fetch failed: 404)");

    [Test]
    public void Each_differing_field_is_named_in_order()
    {
        var h = Handshake();
        h.EntityIdMatches = false;
        h.CertificateMatches = false;

        SsoTestConnectionService.DescribeDifferences(h).Should().Be("entity ID, signing certificate");
    }

    [Test]
    public void All_four_fields_can_differ()
    {
        var h = new HandshakeResult { MetadataFetched = true };

        SsoTestConnectionService.DescribeDifferences(h).Should().Be("entity ID, SSO URL, SLO URL, signing certificate");
    }

    #endregion

    #region Helpers

    private static HandshakeResult Handshake() => new()
    {
        MetadataFetched = true,
        EntityIdMatches = true,
        SsoUrlMatches = true,
        SloUrlMatches = true,
        CertificateMatches = true
    };

    #endregion
}
