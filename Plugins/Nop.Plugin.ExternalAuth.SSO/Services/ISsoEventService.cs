using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Domain;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Writer + reader for the SSO audit log (<c>SSO_AuthEvent</c>).
/// </summary>
/// <remarks>
/// Writes happen after the corresponding side effect succeeds. Writing is fail-safe — an
/// exception while writing the row is logged to nop's <c>ILogger</c> but never propagates, so
/// a logging failure cannot break the login flow. <see cref="SearchAsync"/> and
/// <see cref="ClearAllAsync"/> back the audit-log admin grid and its Clear button.
/// </remarks>
public interface ISsoEventService
{
    /// <summary>
    /// Append a row to <c>SSO_AuthEvent</c>. Returns immediately on failure
    /// (the failure is logged but not propagated).
    /// </summary>
    /// <param name="eventType">One of <c>SsoAuthEventTypes</c></param>
    /// <param name="protocol">One of <c>SsoProtocolNames</c>, or empty when no protocol applies</param>
    /// <param name="customerId">Customer the row is about, when known</param>
    /// <param name="customerEmail">Email the row is about, when known</param>
    /// <param name="idpSubject">IdP subject (<c>sub</c> / NameID), when known</param>
    /// <param name="notes">Free text; failures start with <c>[reason=…]</c></param>
    /// <param name="storeId">Store the row belongs to; null = the current store</param>
    /// <param name="forced">
    /// When <c>true</c>, bypasses the system-wide <c>EnableAuditLogging</c> master switch
    /// so the row is written even when audit logging is disabled. Use for tamper-evidence
    /// breadcrumbs that must always survive — <c>LogCleared</c> writes use the direct
    /// repository path; <c>HandshakeRun</c> rides this parameter.
    /// </param>
    Task LogAsync(
        string eventType,
        string protocol,
        int? customerId = null,
        string? customerEmail = null,
        string? idpSubject = null,
        string? notes = null,
        int? storeId = null,
        bool forced = false);

    /// <summary>
    /// Append a <c>Failed</c> row for the current store, with <paramref name="reason"/> as the
    /// machine-readable <c>[reason=…]</c> prefix of the notes.
    /// </summary>
    /// <param name="protocol">One of <c>SsoProtocolNames</c>, or empty</param>
    /// <param name="reason">One of <c>SsoFailureReasons</c></param>
    /// <param name="detail">Human-readable detail</param>
    /// <param name="customerId">Customer the failure is about, when known</param>
    /// <param name="customerEmail">Email the failure is about, when known</param>
    /// <param name="idpSubject">IdP subject, when known</param>
    Task LogFailureAsync(
        string protocol,
        string reason,
        string detail,
        int? customerId = null,
        string? customerEmail = null,
        string? idpSubject = null);

    /// <summary>
    /// Paged search of the audit log. All filter arguments are optional.
    /// </summary>
    Task<IPagedList<SsoAuthEvent>> SearchAsync(
        int storeId = 0,
        string? eventType = null,
        string? protocol = null,
        string? customerEmail = null,
        DateTime? occurredFromUtc = null,
        DateTime? occurredToUtc = null,
        int pageIndex = 0,
        int pageSize = int.MaxValue);

    /// <summary>
    /// Wipe the audit log. Writes a single <c>LogCleared</c> breadcrumb row first (so the
    /// fact-of-clearing is itself audited), then deletes every other row. Returns the
    /// count of deleted rows.
    /// </summary>
    Task<int> ClearAllAsync();

    /// <summary>
    /// Delete audit rows that occurred before <paramref name="cutoffUtc"/> (retention). Writes no
    /// breadcrumb; the scheduled task that calls it logs to nop's log instead.
    /// </summary>
    /// <param name="cutoffUtc">Rows older than this are deleted</param>
    /// <returns>Number of rows deleted</returns>
    Task<int> PurgeOlderThanAsync(DateTime cutoffUtc);
}
