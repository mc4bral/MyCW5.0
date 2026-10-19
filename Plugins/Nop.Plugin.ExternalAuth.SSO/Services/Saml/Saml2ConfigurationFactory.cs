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

    private readonly ISettingService _settingService;
    private readonly ISsoSecretsService _secretsService;
    private readonly IStoreContext _storeContext;

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
    public async Task<Saml2Configuration> CreateForCurrentStoreAsync()
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
        var idpCertPlaintext = _secretsService.TryUnprotect(settings.IdpCertificateBase64, out var idpCertB64)
            ? idpCertB64
            : throw new InvalidOperationException("Failed to decrypt the IdP signing certificate.");

        config.SignatureValidationCertificates.Add(LoadCertificateFromBase64(idpCertPlaintext));

        // SP PFX — required when AuthnRequest is signed or EncryptedAssertion is on.
        if (settings.SignedRequest || settings.EncryptedAssertion)
        {
            if (string.IsNullOrWhiteSpace(settings.SpPfxBase64))
                throw new InvalidOperationException(
                    "SP signing PFX is required when SignedRequest or EncryptedAssertion is enabled.");

            var pfxBytes = _secretsService.TryUnprotect(settings.SpPfxBase64, out var pfxB64)
                ? Convert.FromBase64String(pfxB64)
                : throw new InvalidOperationException("Failed to decrypt the SP signing PFX.");

            var pfxPassword = _secretsService.TryUnprotect(settings.SpPfxPassword, out var pwd)
                ? pwd
                : string.Empty;

            var spCert = X509CertificateLoader.LoadPkcs12(
                pfxBytes,
                pfxPassword,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

            if (settings.SignedRequest)
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

    #region Utilities

    /// <summary>
    /// Loads an X.509 certificate from a Base64-encoded DER string. Strips PEM headers if
    /// present so admins who paste the PEM form by mistake still succeed.
    /// </summary>
    private static X509Certificate2 LoadCertificateFromBase64(string base64)
    {
        var trimmed = base64
            .Replace("-----BEGIN CERTIFICATE-----", string.Empty)
            .Replace("-----END CERTIFICATE-----", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Trim();

        var bytes = Convert.FromBase64String(trimmed);
        return X509CertificateLoader.LoadCertificate(bytes);
    }

    #endregion
}
