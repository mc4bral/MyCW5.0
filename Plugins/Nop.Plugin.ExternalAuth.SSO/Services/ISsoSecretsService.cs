namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Protects sensitive plugin settings at rest using nopCommerce's <c>IEncryptionService</c>
/// layered with ASP.NET Core Data Protection (<c>IDataProtector</c>).
/// </summary>
/// <remarks>
/// Plain reads/writes of <see cref="Settings.SsoConfigurationSettings"/> sensitive fields
/// (<c>ClientSecret</c>, <c>IdpCertificateBase64</c>, <c>SpPfxBase64</c>, <c>SpPfxPassword</c>)
/// and <see cref="Settings.SsoSystemSettings.SsoBypassToken"/> must round-trip through this
/// service. See SSOPlan.v2.md §3.3.
///
/// Layering: outer = <c>IEncryptionService</c> (nopCommerce-wide static key from
/// <c>appsettings.json</c>); inner = <c>IDataProtector</c> (ASP.NET Core key-ring,
/// auto-rotating). Both must succeed to round-trip; either failure surfaces as
/// <see cref="CryptographicException"/> (fixes legacy P1-16: no silent fallback to plaintext).
/// </remarks>
public interface ISsoSecretsService
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> for at-rest storage.
    /// Returns <see cref="string.Empty"/> when <paramref name="plaintext"/> is null or empty
    /// (so empty settings round-trip as empty rather than ciphertext-of-empty).
    /// </summary>
    string Protect(string? plaintext);

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/> produced by <see cref="Protect"/>.
    /// Returns <see cref="string.Empty"/> when <paramref name="ciphertext"/> is null or empty.
    /// Throws when decryption fails — callers must surface this to the admin rather than
    /// fall back to the raw value.
    /// </summary>
    string Unprotect(string? ciphertext);

    /// <summary>
    /// Non-throwing variant for cold-path code where a decrypt failure should be surfaced
    /// as a structured error rather than an exception. Returns <c>false</c> and leaves
    /// <paramref name="plaintext"/> empty on failure.
    /// </summary>
    bool TryUnprotect(string? ciphertext, out string plaintext);
}
