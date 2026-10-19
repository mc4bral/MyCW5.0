using Microsoft.AspNetCore.DataProtection;
using Nop.Services.Security;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="ISsoSecretsService"/>.
/// </summary>
/// <remarks>
/// Encryption order on <c>Protect</c>:
/// <list type="number">
///   <item><c>IDataProtector.Protect</c> with purpose <see cref="DataProtectionPurpose"/>;</item>
///   <item><c>IEncryptionService.EncryptText</c> using nop's configured private key.</item>
/// </list>
/// Decryption on <c>Unprotect</c> reverses the order.
/// </remarks>
public class SsoSecretsService : ISsoSecretsService
{
    /// <summary>
    /// Data Protection purpose string — never change without a key migration, otherwise
    /// previously-stored ciphertext becomes unreadable.
    /// </summary>
    public const string DataProtectionPurpose = "Nop.Plugin.ExternalAuth.SSO.Settings.v1";

    #region Fields

    private readonly IEncryptionService _encryptionService;
    private readonly IDataProtector _dataProtector;

    #endregion

    #region Ctor

    public SsoSecretsService(
        IEncryptionService encryptionService,
        IDataProtectionProvider dataProtectionProvider)
    {
        _encryptionService = encryptionService;
        _dataProtector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public string Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return string.Empty;

        var protectedText = _dataProtector.Protect(plaintext);
        return _encryptionService.EncryptText(protectedText);
    }

    /// <inheritdoc />
    public string Unprotect(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return string.Empty;

        // Outer layer first — nop's IEncryptionService throws if the configured key cannot decrypt.
        var protectedText = _encryptionService.DecryptText(ciphertext);

        // Inner layer — IDataProtector throws CryptographicException on key-ring mismatch.
        return _dataProtector.Unprotect(protectedText);
    }

    /// <inheritdoc />
    public bool TryUnprotect(string? ciphertext, out string plaintext)
    {
        try
        {
            plaintext = Unprotect(ciphertext);
            return true;
        }
        catch
        {
            plaintext = string.Empty;
            return false;
        }
    }

    #endregion
}
