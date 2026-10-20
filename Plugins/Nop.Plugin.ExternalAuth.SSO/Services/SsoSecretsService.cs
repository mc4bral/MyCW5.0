using Microsoft.AspNetCore.DataProtection;
using Nop.Services.Security;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="ISsoSecretsService"/>.
/// </summary>
/// <remarks>
/// Current format: <c>dp1:</c> + Data Protection ciphertext. The prefix says how a value was
/// written, so the format can change again without guessing. Legacy format (no prefix): Data
/// Protection ciphertext wrapped in nop's <c>IEncryptionService</c>; read only, rewritten by the
/// plugin update (<c>SSOAuthenticationMethod.ReprotectLegacySecretsAsync</c>). Once no install
/// holds legacy values, the legacy branch and the <see cref="IEncryptionService"/> dependency can go.
/// </remarks>
public class SsoSecretsService : ISsoSecretsService
{
    #region Fields

    /// <summary>Data Protection purpose — changing it makes every stored secret unreadable.</summary>
    public const string DataProtectionPurpose = "Nop.Plugin.ExternalAuth.SSO.Settings.v1";

    /// <summary>Prefix of values in the current single-layer format.</summary>
    public const string CurrentFormatPrefix = "dp1:";

    protected readonly IEncryptionService _encryptionService;
    protected readonly IDataProtector _dataProtector;

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

        return CurrentFormatPrefix + _dataProtector.Protect(plaintext);
    }

    /// <inheritdoc />
    public string Unprotect(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return string.Empty;

        if (ciphertext.StartsWith(CurrentFormatPrefix, StringComparison.Ordinal))
            return _dataProtector.Unprotect(ciphertext[CurrentFormatPrefix.Length..]);

        // Legacy: nop's IEncryptionService outside, Data Protection inside.
        return _dataProtector.Unprotect(_encryptionService.DecryptText(ciphertext));
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

    /// <inheritdoc />
    public bool IsCurrentFormat(string? ciphertext)
        => string.IsNullOrEmpty(ciphertext) || ciphertext.StartsWith(CurrentFormatPrefix, StringComparison.Ordinal);

    #endregion
}
