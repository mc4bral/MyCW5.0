using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Services.Http;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using NUnit.Framework;

namespace CW.Tests.ExternalAuth.SSO;

/// <summary>
/// SAML settings helpers, certificate loading, and the outbound-URL guard. IP literals keep the
/// URL tests off the network (no DNS lookup).
/// </summary>
[TestFixture]
public class SamlAndUrlTests
{
    #region SAML binding

    [TestCase("HTTP-POST", SamlBinding.HttpPost)]
    [TestCase("HttpPost", SamlBinding.HttpPost)]
    [TestCase("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST", SamlBinding.HttpPost)]
    [TestCase("HTTP-Redirect", SamlBinding.HttpRedirect)]
    [TestCase("HttpRedirect", SamlBinding.HttpRedirect)]
    [TestCase(null, SamlBinding.HttpRedirect, TestName = "Missing binding defaults to HTTP-Redirect")]
    [TestCase("", SamlBinding.HttpRedirect)]
    public void Binding_parse(string? value, SamlBinding expected)
        => SamlBindingExtensions.Parse(value).Should().Be(expected);

    [Test]
    public void Binding_round_trips_through_its_display_name()
    {
        foreach (var binding in Enum.GetValues<SamlBinding>())
            SamlBindingExtensions.Parse(binding.ToDisplayName()).Should().Be(binding);
    }

    #endregion

    #region Certificates

    [Test]
    public void Certificate_loads_from_base64_and_from_pem()
    {
        using var cert = CreateCertificate();
        var base64 = Convert.ToBase64String(cert.RawData);
        var pem = "-----BEGIN CERTIFICATE-----\n" + base64 + "\n-----END CERTIFICATE-----\n";

        SsoCertificates.TryLoad(base64, out var fromBase64, out _).Should().BeTrue();
        SsoCertificates.TryLoad(pem, out var fromPem, out _).Should().BeTrue();
        fromBase64!.Thumbprint.Should().Be(cert.Thumbprint);
        fromPem!.Thumbprint.Should().Be(cert.Thumbprint);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not base64 at all!")]
    [TestCase("AAAA")]
    public void Certificate_load_fails_cleanly_on_bad_input(string? value)
    {
        SsoCertificates.TryLoad(value, out var certificate, out var error).Should().BeFalse();
        certificate.Should().BeNull();
        error.Should().NotBeEmpty();
    }

    [Test]
    public void Sha256_thumbprint_is_64_hex_characters()
    {
        using var cert = CreateCertificate();

        SsoCertificates.Sha256Thumbprint(cert).Should().MatchRegex("^[0-9A-F]{64}$");
    }

    #endregion

    #region Outbound URL guard

    [TestCase("10.1.2.3")]
    [TestCase("172.16.0.1")]
    [TestCase("172.31.255.255")]
    [TestCase("192.168.1.1")]
    [TestCase("169.254.169.254", TestName = "Cloud metadata address is refused")]
    [TestCase("127.0.0.1")]
    [TestCase("0.0.0.0")]
    [TestCase("::1")]
    [TestCase("fe80::1")]
    [TestCase("fd00::1")]
    [TestCase("::ffff:10.0.0.1", TestName = "IPv4-mapped private address is refused")]
    public void Private_and_reserved_addresses_are_refused(string address)
        => SafeUrl.IsPrivateOrReservedAddress(IPAddress.Parse(address)).Should().BeTrue();

    [TestCase("8.8.8.8")]
    [TestCase("172.32.0.1", TestName = "Just outside 172.16.0.0/12")]
    [TestCase("2001:4860:4860::8888")]
    public void Public_addresses_are_allowed(string address)
        => SafeUrl.IsPrivateOrReservedAddress(IPAddress.Parse(address)).Should().BeFalse();

    [TestCase("http://8.8.8.8/metadata", "HTTPS", TestName = "Plain HTTP is refused")]
    [TestCase("https://127.0.0.1/metadata", "local host")]
    [TestCase("https://10.0.0.5/metadata", "private")]
    [TestCase("not a url", "absolute URL")]
    public void Unsafe_urls_are_refused_with_a_reason(string url, string reason)
    {
        SafeUrl.TryValidate(url, "Metadata URL", out var error).Should().BeFalse();
        error.Should().Contain(reason);
    }

    [Test]
    public void A_public_https_address_is_allowed()
    {
        SafeUrl.TryValidate("https://8.8.8.8/metadata", "Metadata URL", out var error).Should().BeTrue();
        error.Should().BeEmpty();
    }

    #endregion

    #region Helpers

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=cw-tests", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    #endregion
}
