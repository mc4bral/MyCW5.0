using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Domain;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Writer + reader for the SSO audit log (<c>SSO_AuthEvent</c>).
/// </summary>
/// <remarks>
/// Per SSOPlan.v2.md §3.9: writes happen after the corresponding side effect succeeds.
/// <see cref="LogAsync"/> is fail-safe — an exception while writing the row is logged
/// to <c>ILogger</c> but never propagates, so a logging failure cannot break the login
/// flow (fixes legacy P1-10).
///
/// M10 added <see cref="SearchAsync"/> and <see cref="ClearAllAsync"/> to back the
/// audit-log admin grid + Clear button.
/// </remarks>
public interface ISsoEventService
{
    /// <summary>
    /// Append a row to <c>SSO_AuthEvent</c>. Returns immediately on failure
    /// (the failure is logged but not propagated).
    /// </summary>
    /// <param name="forced">
    /// When <c>true</c>, bypasses the system-wide <c>EnableAuditLogging</c> master switch
    /// so the row is written even when audit logging is disabled. Use for tamper-evidence
    /// breadcrumbs that must always survive — <c>LogCleared</c> writes use the direct
    /// repository path; <c>HandshakeRun</c> rides this parameter.
    /// </param>
    Task LogAsync(
        string eventType,
        string protocol,
        int storeId,
        int? customerId,
        string customerEmail,
        string idpSubject,
        string? notes = null,
        bool forced = false);

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
}
