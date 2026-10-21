using System.Net;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Http;

/// <summary>
/// Guard for URLs the plugin fetches on an admin's behalf (IdP metadata, OIDC discovery,
/// test-connection probes), so they cannot be used to reach the server's own network.
/// </summary>
/// <remarks>
/// HTTPS only; the host must not be loopback and must resolve only to public addresses.
/// Best effort: DNS rebinding can defeat a resolve-then-fetch check, so production should also
/// restrict outbound traffic at the network edge.
/// </remarks>
public static class SafeUrl
{
    #region Utilities

    internal static bool IsPrivateOrReservedAddress(IPAddress addr)
    {
        if (addr.IsIPv4MappedToIPv6)
            addr = addr.MapToIPv4();

        if (IPAddress.IsLoopback(addr)) return true;

        var bytes = addr.GetAddressBytes();

        if (bytes.Length == 4)
        {
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 127.0.0.0/8 (loopback)
            if (bytes[0] == 127) return true;
            // 0.0.0.0/8
            if (bytes[0] == 0) return true;
        }

        // IPv6 link-local / site-local / ULA / loopback
        if (addr.IsIPv6LinkLocal || addr.IsIPv6SiteLocal) return true;
        if (bytes.Length == 16 && (bytes[0] & 0xFE) == 0xFC) return true; // fc00::/7

        return false;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Throw <see cref="InvalidOperationException"/> when the URL is not safe to fetch.
    /// </summary>
    /// <param name="uri">URL to check</param>
    /// <param name="what">What the URL is, for the message (e.g. "Metadata URL")</param>
    public static void EnsureSafe(Uri uri, string what)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException($"{what} must use HTTPS.");

        if (uri.IsLoopback)
            throw new InvalidOperationException($"{what} must not point at the local host.");

        IPAddress[] addresses;
        try
        {
            addresses = Dns.GetHostAddresses(uri.Host);
        }
        catch
        {
            throw new InvalidOperationException($"{what} host could not be resolved.");
        }

        foreach (var addr in addresses)
        {
            if (IsPrivateOrReservedAddress(addr))
                throw new InvalidOperationException($"{what} host resolves to a private or reserved address ({addr}).");
        }
    }

    /// <summary>
    /// Check a URL without throwing.
    /// </summary>
    /// <param name="url">URL to check</param>
    /// <param name="what">What the URL is, for the message</param>
    /// <param name="error">Why it was refused; empty when safe</param>
    /// <returns>True when the URL may be fetched</returns>
    public static bool TryValidate(string? url, string what, out string error)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            error = $"{what} is not a valid absolute URL.";
            return false;
        }

        try
        {
            EnsureSafe(uri, what);
            error = string.Empty;
            return true;
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    #endregion
}
