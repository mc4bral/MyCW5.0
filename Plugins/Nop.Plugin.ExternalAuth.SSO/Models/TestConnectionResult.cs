namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// Result of <see cref="Services.ISsoTestConnectionService.TestAsync"/>.
/// Serialised to JSON for the inline status next to the Test button on the SSO Accounts grid.
/// </summary>
public record TestConnectionResult
{
    /// <summary><c>true</c> when the test succeeded end-to-end.</summary>
    public bool Success { get; set; }

    /// <summary>One-line summary suitable for an inline badge / toast.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional multi-line detail (cert subject / discovery issuer / HTTP body) for the tooltip.</summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>When the test ran. Useful when the admin re-runs it and wants to see the freshness.</summary>
    public DateTime CheckedAtUtc { get; set; }

    /// <summary>
    /// outcome of the IdP-metadata handshake. Always populated; <see cref="HandshakeResult.SkippedReason"/>
    /// is set when no metadata URL is configured (OIDC or SAML without <c>IdpMetadataUrl</c>).
    /// </summary>
    public HandshakeResult Handshake { get; set; } = new HandshakeResult();
}
