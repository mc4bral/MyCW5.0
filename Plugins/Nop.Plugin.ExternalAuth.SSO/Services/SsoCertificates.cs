using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Nop.Plugin.ExternalAuth.SSO.Settings;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Loading the plugin's certificates: the IdP signing certificate (stored encrypted as Base64 DER,
/// sometimes pasted as PEM) and the SP PFX. One place, so every caller parses them the same way.
/// </summary>
/// <remarks>
/// Every <see cref="X509Certificate2"/> returned here holds a native handle; callers dispose it
/// (<c>using var</c>) unless they hand it to ITfoxtec's configuration, which keeps it.
/// </remarks>
public static class SsoCertificates
{
    #region Utilities

    /// <summary>
    /// Base64 DER from a value that may carry PEM armour and line breaks.
    /// </summary>
    private static string StripPem(string value)
    {
        var withoutArmour = value
            .Replace("-----BEGIN CERTIFICATE-----", string.Empty)
            .Replace("-----END CERTIFICATE-----", string.Empty);

        return new string(withoutArmour.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    #endregion

    #region Methods

    /// <summary>
    /// Load an X.509 certificate from Base64 DER or PEM text.
    /// </summary>
    /// <param name="base64OrPem">Certificate text</param>
    /// <returns>The certificate</returns>
    /// <exception cref="FormatException">Not Base64</exception>
    /// <exception cref="CryptographicException">Not a certificate</exception>
    public static X509Certificate2 Load(string base64OrPem)
        => X509CertificateLoader.LoadCertificate(Convert.FromBase64String(StripPem(base64OrPem)));

    /// <summary>
    /// Load an X.509 certificate from Base64 DER or PEM text without throwing.
    /// </summary>
    /// <param name="base64OrPem">Certificate text</param>
    /// <param name="certificate">The certificate, when it loaded</param>
    /// <param name="error">Why it did not load; empty on success</param>
    /// <returns>True when the certificate loaded</returns>
    public static bool TryLoad(string? base64OrPem, out X509Certificate2? certificate, out string error)
    {
        certificate = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(base64OrPem))
        {
            error = "No certificate.";
            return false;
        }

        try
        {
            certificate = Load(base64OrPem);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Decrypt and load a store's IdP signing certificate.
    /// </summary>
    /// <param name="secrets">Secrets service that decrypts the stored value</param>
    /// <param name="settings">The store's settings</param>
    /// <param name="certificate">The certificate, when it loaded</param>
    /// <param name="error">Why it did not load (not configured, not decryptable, not a certificate); empty on success</param>
    /// <returns>True when the certificate loaded</returns>
    public static bool TryLoadIdpCertificate(ISsoSecretsService secrets, SsoConfigurationSettings settings,
        out X509Certificate2? certificate, out string error)
    {
        certificate = null;

        if (string.IsNullOrWhiteSpace(settings.IdpCertificateBase64))
        {
            error = "IdP signing certificate is not configured.";
            return false;
        }

        if (!secrets.TryUnprotect(settings.IdpCertificateBase64, out var plaintext) || string.IsNullOrWhiteSpace(plaintext))
        {
            error = "IdP signing certificate could not be decrypted.";
            return false;
        }

        if (!TryLoad(plaintext, out certificate, out var loadError))
        {
            error = $"IdP signing certificate is not a valid X.509 certificate (Base64 DER or PEM): {loadError}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Decrypt and load a store's SP PFX (signing / decryption key pair).
    /// </summary>
    /// <param name="secrets">Secrets service that decrypts the stored values</param>
    /// <param name="settings">The store's settings</param>
    /// <returns>The certificate with its private key</returns>
    /// <exception cref="InvalidOperationException">Not configured or not decryptable</exception>
    public static X509Certificate2 LoadSpPfx(ISsoSecretsService secrets, SsoConfigurationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SpPfxBase64))
            throw new InvalidOperationException("SP signing PFX is not configured.");

        if (!secrets.TryUnprotect(settings.SpPfxBase64, out var pfxBase64))
            throw new InvalidOperationException("Failed to decrypt the SP signing PFX.");

        var password = secrets.TryUnprotect(settings.SpPfxPassword, out var pwd) ? pwd : string.Empty;

        return X509CertificateLoader.LoadPkcs12(
            Convert.FromBase64String(StripPem(pfxBase64)),
            password,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }

    /// <summary>
    /// SHA-256 thumbprint of the certificate's DER, uppercase hex without separators. Used instead
    /// of <see cref="X509Certificate2.Thumbprint"/>, which is SHA-1.
    /// </summary>
    /// <param name="certificate">Certificate</param>
    public static string Sha256Thumbprint(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        return Convert.ToHexString(SHA256.HashData(certificate.RawData));
    }

    #endregion
}
