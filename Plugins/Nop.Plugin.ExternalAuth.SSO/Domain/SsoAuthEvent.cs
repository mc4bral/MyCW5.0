using Nop.Core;

namespace Nop.Plugin.ExternalAuth.SSO.Domain;

/// <summary>
/// Append-only audit row for an SSO authentication event.
/// </summary>
/// <remarks>
/// Persisted to <c>SSO_AuthEvent</c> (see <see cref="Data.ExternalAuthSSONameCompatibility"/>).
/// Written by the user-provisioning pipeline after every login attempt, link, auto-registration,
/// SLO, bypass-denial, or failure. See References/SSO_Plugin_Reference.md §3.
/// </remarks>
public class SsoAuthEvent : BaseEntity
{
    /// <summary>
    /// Identifier of the store that processed the event.
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Identifier of the nopCommerce customer resolved from the IdP subject, when one could be resolved.
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// Email address asserted by the IdP for this event.
    /// </summary>
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>
    /// Event type discriminator (e.g. <c>Login</c>, <c>AutoRegistered</c>, <c>Linked</c>, <c>SignedIn</c>, <c>Slo</c>,
    /// <c>Failed</c>, <c>BypassDenied</c>).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Protocol the event came from (<c>Oidc</c> or <c>Saml2</c>).
    /// </summary>
    public string Protocol { get; set; } = string.Empty;

    /// <summary>
    /// Subject identifier from the IdP — OIDC <c>sub</c> claim or SAML <c>NameID</c>.
    /// </summary>
    public string IdpSubject { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the event was recorded.
    /// </summary>
    public DateTime OccurredOnUtc { get; set; }

    /// <summary>
    /// Client IP address observed at the time of the event.
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Free-form error detail or contextual information. Nullable.
    /// </summary>
    public string? Notes { get; set; }
}
