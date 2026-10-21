using System.Security.Cryptography.X509Certificates;
using ITfoxtec.Identity.Saml2;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Default implementation of <see cref="ISaml2ConfigurationFactory"/>.
/// </summary>
public class Saml2ConfigurationFactory : ISaml2ConfigurationFactory
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly ISsoSecretsService _secretsService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public Saml2ConfigurationFactory(
        ISettingService settingService,
        ISsoSecretsService secretsService,
        IStoreContext storeContext)
    {
        _settingService = settingService;
        _secretsService = secretsService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<Saml2Configuration> CreateForCurrentStoreAsync(SamlConfigPurpose purpose = SamlConfigPurpose.Login)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);

        if (string.IsNullOrWhiteSpace(settings.IdpEntityId)
            || string.IsNullOrWhiteSpace(settings.IdpSsoUrl)
            || string.IsNullOrWhiteSpace(settings.IdpCertificateBase64)
            || string.IsNullOrWhiteSpace(settings.SpEntityId))
        {
            throw new InvalidOperationException(
                "SAML configuration incomplete. IdP entity ID, IdP SSO URL, IdP certificate, and SP entity ID are required.");
        }

        var config = new Saml2Configuration
        {
            Issuer = settings.SpEntityId,
            AllowedIssuer = settings.IdpEntityId,
            SingleSignOnDestination = new Uri(settings.IdpSsoUrl),
            SignAuthnRequest = settings.SignedRequest
        };
        // Note: ITfoxtec.Identity.Saml2 v4.x does not expose a clock-skew slot on
        // Saml2Configuration; the library applies its own internal allowance during
        // assertion validation. If we need a custom skew later, we'll have to override
        // the validator pipeline.

        config.AllowedAudienceUris.Add(settings.SpEntityId);

        // The ACS URL goes on the AuthnRequest in SamlChallengeBuilder rather than on
        // Saml2Configuration (which doesn't expose a strongly-typed slot for it).

        if (!string.IsNullOrWhiteSpace(settings.IdpSloUrl)
            && Uri.TryCreate(settings.IdpSloUrl, UriKind.Absolute, out var sloUri))
        {
            config.SingleLogoutDestination = sloUri;
        }

        // IdP signing certificate — required for asserting signature/audience.
        if (!SsoCertificates.TryLoadIdpCertificate(_secretsService, settings, out var idpCertificate, out var certError))
            throw new InvalidOperationException(certError);

        config.SignatureValidationCertificates.Add(idpCertificate!);

        // Trust is the pinned certificate above: ITfoxtec verifies a signature only against
        // SignatureValidationCertificates, never against a certificate the message carries. IdP
        // signing certificates are self-signed (Entra, Keycloak, ADFS), so the default ChainTrust
        // refused every response ("terminated in a root certificate which is not trusted" — found
        // against Keycloak, review item C1) and online revocation checks have nothing to check.
        // With None the library does not check expiry either: an expired pinned certificate is
        // still accepted, as is usual for pinned SAML certificates. System → Warnings reports it
        // before and after it expires (§7.4 of the reference).
        config.CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None;
        config.RevocationMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;

        // Whether this configuration signs what it binds. ITfoxtec signs any message whose
        // configuration carries a SigningCertificate, so the certificate is attached only for
        // the purpose whose flag is on — logout signing is independent of AuthnRequest signing.
        var sign = purpose switch
        {
            SamlConfigPurpose.Logout => settings.SignLogoutRequest,
            SamlConfigPurpose.Metadata => settings.SignedRequest || settings.SignLogoutRequest,
            _ => settings.SignedRequest
        };

        // SP PFX — required when this configuration signs or EncryptedAssertion is on.
        if (sign || settings.EncryptedAssertion)
        {
            if (string.IsNullOrWhiteSpace(settings.SpPfxBase64))
                throw new InvalidOperationException(
                    "SP signing PFX is required when request signing or EncryptedAssertion is enabled.");

            var spCert = SsoCertificates.LoadSpPfx(_secretsService, settings);

            if (sign)
                config.SigningCertificate = spCert;

            if (settings.EncryptedAssertion)
                config.DecryptionCertificates.Add(spCert);
        }

        // Stash the absolute ACS URL on the config for the challenge builder to use.
        // ITfoxtec doesn't have a strongly-typed slot for it on Saml2Configuration; the
        // builder reads SsoConfigurationSettings directly when constructing the AuthnRequest.

        return config;
    }

    #endregion
}
