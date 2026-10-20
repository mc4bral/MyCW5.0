using Nop.Core;
using Nop.Data;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Domain;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Logging;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="ISsoEventService"/>.
/// </summary>
public class SsoEventService : ISsoEventService
{
    #region Fields

    protected readonly IRepository<SsoAuthEvent> _repository;
    protected readonly ILogger _logger;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWebHelper _webHelper;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public SsoEventService(
        IRepository<SsoAuthEvent> repository,
        ILogger logger,
        ISettingService settingService,
        IStoreContext storeContext,
        IWebHelper webHelper,
        IWorkContext workContext)
    {
        _repository = repository;
        _logger = logger;
        _settingService = settingService;
        _storeContext = storeContext;
        _webHelper = webHelper;
        _workContext = workContext;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public Task LogFailureAsync(
        string protocol,
        string reason,
        string detail,
        int? customerId = null,
        string? customerEmail = null,
        string? idpSubject = null)
        => LogAsync(
            eventType: SsoAuthEventTypes.Failed,
            protocol: protocol,
            customerId: customerId,
            customerEmail: customerEmail,
            idpSubject: idpSubject,
            notes: SsoFailureReasons.Format(reason, detail));

    /// <inheritdoc />
    public async Task LogAsync(
        string eventType,
        string protocol,
        int? customerId = null,
        string? customerEmail = null,
        string? idpSubject = null,
        string? notes = null,
        int? storeId = null,
        bool forced = false)
    {
        // master kill switch for audit writes. Two exemptions:
        //  (1) `forced: true` callers — e.g. HandshakeRun which must record every
        //      test-connection click as tamper evidence.
        //  (2) LogCleared kept here as belt-and-braces; the ClearAllAsync path writes
        //      direct to the repository today but the named-event check stays in case a
        //      future caller routes LogCleared through LogAsync.
        if (!forced
            && !string.Equals(eventType, SsoAuthEventTypes.LogCleared, StringComparison.Ordinal))
        {
            var systemSettings = await _settingService.LoadSettingAsync<SsoSystemSettings>(storeId: 0);
            if (!systemSettings.EnableAuditLogging)
                return;
        }

        try
        {
            var entity = new SsoAuthEvent
            {
                EventType = eventType ?? string.Empty,
                Protocol = protocol ?? string.Empty,
                StoreId = storeId ?? (await _storeContext.GetCurrentStoreAsync()).Id,
                CustomerId = customerId,
                CustomerEmail = customerEmail ?? string.Empty,
                IdpSubject = idpSubject ?? string.Empty,
                IpAddress = _webHelper.GetCurrentIpAddress() ?? string.Empty,
                OccurredOnUtc = DateTime.UtcNow,
                Notes = notes
            };

            await _repository.InsertAsync(entity);
        }
        catch (Exception ex)
        {
            // Logging failure must never break login. Surface to ILogger and return.
            await _logger.ErrorAsync($"ExternalAuth.SSO: failed to write {eventType} audit row for {customerEmail}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<IPagedList<SsoAuthEvent>> SearchAsync(
        int storeId = 0,
        string? eventType = null,
        string? protocol = null,
        string? customerEmail = null,
        DateTime? occurredFromUtc = null,
        DateTime? occurredToUtc = null,
        int pageIndex = 0,
        int pageSize = int.MaxValue)
    {
        return await _repository.GetAllPagedAsync(query =>
        {
            if (storeId > 0)
                query = query.Where(e => e.StoreId == storeId);

            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(e => e.EventType == eventType);

            if (!string.IsNullOrEmpty(protocol))
                query = query.Where(e => e.Protocol == protocol);

            if (!string.IsNullOrEmpty(customerEmail))
                query = query.Where(e => e.CustomerEmail.Contains(customerEmail));

            if (occurredFromUtc.HasValue)
                query = query.Where(e => e.OccurredOnUtc >= occurredFromUtc.Value);

            if (occurredToUtc.HasValue)
                query = query.Where(e => e.OccurredOnUtc <= occurredToUtc.Value);

            return query.OrderByDescending(e => e.OccurredOnUtc).ThenByDescending(e => e.Id);
        }, pageIndex, pageSize);
    }

    /// <inheritdoc />
    public async Task<int> ClearAllAsync()
    {
        // First write the breadcrumb so the act of clearing is itself recorded.
        var admin = await _workContext.GetCurrentCustomerAsync();
        var breadcrumb = new SsoAuthEvent
        {
            EventType = SsoAuthEventTypes.LogCleared,
            Protocol = string.Empty,
            StoreId = 0,
            CustomerId = admin?.Id,
            CustomerEmail = admin?.Email ?? string.Empty,
            IdpSubject = string.Empty,
            IpAddress = _webHelper.GetCurrentIpAddress() ?? string.Empty,
            OccurredOnUtc = DateTime.UtcNow,
            Notes = $"Audit log cleared by {admin?.Email ?? "<unknown admin>"}."
        };
        await _repository.InsertAsync(breadcrumb);

        // Delete every row except the breadcrumb we just inserted.
        var deletedCount = 0;
        try
        {
            deletedCount = await _repository.DeleteAsync(e => e.Id != breadcrumb.Id);
        }
        catch (Exception ex)
        {
            await _logger.ErrorAsync("ExternalAuth.SSO: ClearAllAsync bulk delete failed", ex);
        }

        return deletedCount;
    }

    /// <inheritdoc />
    public async Task<int> PurgeOlderThanAsync(DateTime cutoffUtc)
        => await _repository.DeleteAsync(e => e.OccurredOnUtc < cutoffUtc);

    #endregion
}
