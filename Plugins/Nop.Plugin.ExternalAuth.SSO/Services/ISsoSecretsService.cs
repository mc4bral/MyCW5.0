namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Protects the plugin's secret settings at rest with ASP.NET Core Data Protection.
/// </summary>
/// <remarks>
/// <para>
/// Applies to <see cref="Settings.SsoConfigurationSettings"/> <c>ClientSecret</c>,
/// <c>IdpCertificateBase64</c>, <c>SpPfxBase64</c>, <c>SpPfxPassword</c> and
/// <see cref="Settings.SsoSystemSettings.SsoBypassToken"/>; reads and writes of those fields go
/// through this service.
/// </para>
/// <para>
/// One layer: the Data Protection key ring, which nop keeps outside the database
/// (<c>App_Data/DataProtectionKeys</c>, or Redis / Azure when configured). Values written before
/// 2026-10-19 were also wrapped in nop's <c>IEncryptionService</c>, whose key lives in the same
/// database as the values; they are still read, and the plugin update rewrites them. A value that
/// cannot be decrypted is an error, never a silent fallback to the raw text.
/// </para>
/// </remarks>
public interface ISsoSecretsService
{
    /// <summary>
    /// Encrypt <paramref name="plaintext"/> for storage. Empty in, empty out (an empty setting
    /// stays empty rather than becoming ciphertext-of-empty).
    /// </summary>
    string Protect(string? plaintext);

    /// <summary>
    /// Decrypt a stored value, current or legacy format. Empty in, empty out.
    /// </summary>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The value cannot be decrypted with this server's keys</exception>
    string Unprotect(string? ciphertext);

    /// <summary>
    /// Non-throwing <see cref="Unprotect"/>: false, with an empty <paramref name="plaintext"/>, when
    /// the value cannot be decrypted.
    /// </summary>
    bool TryUnprotect(string? ciphertext, out string plaintext);

    /// <summary>
    /// Whether a stored value is empty or already in the current single-layer format.
    /// </summary>
    bool IsCurrentFormat(string? ciphertext);
}
