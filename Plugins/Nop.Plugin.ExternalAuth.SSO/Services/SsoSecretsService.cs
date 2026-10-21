using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="ISsoSecretsService"/>.
/// </summary>
/// <remarks>
/// Stored format: <c>dp1:</c> + Data Protection ciphertext. The prefix says how a value was written,
/// so the format can change again without guessing. A value without it (the two-layer format used
/// before 2026-10-19, removed on 2026-10-20) is refused like any value this server cannot decrypt.
/// </remarks>
public class SsoSecretsService : ISsoSecretsService
{
    #region Fields

    /// <summary>Data Protection purpose — changing it makes every stored secret unreadable.</summary>
    public const string DataProtectionPurpose = "Nop.Plugin.ExternalAuth.SSO.Settings.v1";

    /// <summary>Prefix of every stored value.</summary>
    public const string CurrentFormatPrefix = "dp1:";

    protected readonly IDataProtector _dataProtector;

    #endregion

    #region Ctor

    public SsoSecretsService(IDataProtectionProvider dataProtectionProvider)
    {
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

        if (!ciphertext.StartsWith(CurrentFormatPrefix, StringComparison.Ordinal))
            throw new CryptographicException("The stored SSO secret is not in the current format; re-enter it on the Configure page.");

        return _dataProtector.Unprotect(ciphertext[CurrentFormatPrefix.Length..]);
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
