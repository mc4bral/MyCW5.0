using System.Security.Cryptography;
using AwesomeAssertions;
using Microsoft.AspNetCore.DataProtection;
using Nop.Plugin.ExternalAuth.SSO.Services;
using NUnit.Framework;

namespace CW.Tests.ExternalAuth.SSO;

[TestFixture]
public class SsoSecretsServiceTests
{
    private SsoSecretsService _service = null!;

    [SetUp]
    public void SetUp() => _service = new SsoSecretsService(new EphemeralDataProtectionProvider());

    [Test]
    public void Protect_then_Unprotect_returns_the_original_value()
    {
        var stored = _service.Protect("client-secret-value");

        stored.Should().StartWith(SsoSecretsService.CurrentFormatPrefix);
        stored.Should().NotContain("client-secret-value");
        _service.Unprotect(stored).Should().Be("client-secret-value");
    }

    [TestCase(null)]
    [TestCase("")]
    public void Empty_stays_empty(string? value)
    {
        _service.Protect(value).Should().BeEmpty();
        _service.Unprotect(value).Should().BeEmpty();
    }

    [Test]
    public void A_value_without_the_format_prefix_is_refused()
    {
        var act = () => _service.Unprotect("CfDJ8-old-two-layer-value");

        act.Should().Throw<CryptographicException>();
        _service.TryUnprotect("CfDJ8-old-two-layer-value", out var plaintext).Should().BeFalse();
        plaintext.Should().BeEmpty();
    }

    [Test]
    public void A_value_from_another_key_ring_is_refused()
    {
        var other = new SsoSecretsService(new EphemeralDataProtectionProvider());
        var stored = other.Protect("secret");

        _service.TryUnprotect(stored, out var plaintext).Should().BeFalse();
        plaintext.Should().BeEmpty();
    }
}
