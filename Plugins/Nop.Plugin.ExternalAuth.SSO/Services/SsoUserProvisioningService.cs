using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Http;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Authentication.External;
using Nop.Services.Configuration;
using Nop.Services.Customers;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="ISsoUserProvisioningService"/>.
/// </summary>
public class SsoUserProvisioningService : ISsoUserProvisioningService
{
    #region Constants

    /// <summary>
    /// Role system names that are never allowed to be linked to or auto-assigned from SSO.
    /// Enforced both here (admin-account link block) and in the group→role resolver (M3).
    /// </summary>
    private static readonly string[] PrivilegedRoleSystemNames =
    {
        "Administrators"
    };

    #endregion

    #region Fields

    private readonly ICustomerService _customerService;
    private readonly IExternalAuthenticationService _externalAuthenticationService;
    private readonly ISettingService _settingService;
    private readonly ISsoEventService _eventService;
    private readonly ISsoGroupRoleMappingService _groupRoleMappingService;
    private readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public SsoUserProvisioningService(
        ICustomerService customerService,
        IExternalAuthenticationService externalAuthenticationService,
        ISettingService settingService,
        ISsoEventService eventService,
        ISsoGroupRoleMappingService groupRoleMappingService,
        IStoreContext storeContext)
    {
        _customerService = customerService;
        _externalAuthenticationService = externalAuthenticationService;
        _settingService = settingService;
        _eventService = eventService;
        _groupRoleMappingService = groupRoleMappingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IActionResult> HandleAsync(
        ClaimsPrincipal principal,
        string protocolName,
        string? accessToken,
        string? returnUrl)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);

        // 1. Extract claims using per-store mapping; fall back to ASP.NET Core's
        //    canonical schema URIs when the configured claim type isn't present.
        var email = FindFirst(principal, settings.EmailClaimType, ClaimTypes.Email);
        var firstName = FindFirst(principal, settings.FirstNameClaimType, ClaimTypes.GivenName);
        var lastName = FindFirst(principal, settings.LastNameClaimType, ClaimTypes.Surname);
        var username = FindFirst(principal, settings.UsernameClaimType, ClaimTypes.Name);
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        // Groups are read for audit visibility but NOT mapped to roles in M4. M3 swaps
        // the empty list below for the result of ISsoGroupRoleMappingService.ResolveCustomerRoleIdsAsync.
        var groups = principal.FindAll(c => c.Type == settings.GroupsClaimType)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();

        // 2. Validate we have a usable email.
        if (string.IsNullOrWhiteSpace(email))
        {
            await _eventService.LogAsync(
                eventType: SsoAuthEventTypes.Failed,
                protocol: protocolName,
                storeId: store.Id,
                customerId: null,
                customerEmail: string.Empty,
                idpSubject: subject,
                notes: SsoFailureReasons.Format(SsoFailureReasons.AcsNoEmailClaim, "No email claim present in the IdP token."));
            return RedirectToLogin(returnUrl);
        }

        // 3. Email-domain allowlist.
        if (!IsEmailDomainAllowed(email, settings.AllowedEmailDomains))
        {
            await _eventService.LogAsync(
                eventType: SsoAuthEventTypes.Failed,
                protocol: protocolName,
                storeId: store.Id,
                customerId: null,
                customerEmail: email,
                idpSubject: subject,
                notes: SsoFailureReasons.Format(SsoFailureReasons.AcsEmailDomainBlocked,
                    $"Email domain rejected by allowlist '{settings.AllowedEmailDomains}'."));
            return RedirectToLogin(returnUrl);
        }

        // 4. Admin-account-link block.
        var customerBefore = await _customerService.GetCustomerByEmailAsync(email);
        if (customerBefore != null && await IsPrivilegedAsync(customerBefore))
        {
            await _eventService.LogAsync(
                eventType: SsoAuthEventTypes.Failed,
                protocol: protocolName,
                storeId: store.Id,
                customerId: customerBefore.Id,
                customerEmail: email,
                idpSubject: subject,
                notes: SsoFailureReasons.Format(SsoFailureReasons.AcsAdminAccountBlocked,
                    "Existing customer is in a privileged role — SSO link rejected."));
            return RedirectToLogin(returnUrl);
        }

        // 5. Build parameters for nop's external-auth pipeline.
        var parameters = new ExternalAuthenticationParameters
        {
            ProviderSystemName = SSOAuthenticationDefaults.SystemName,
            AccessToken = accessToken,
            Email = email,
            ExternalIdentifier = subject,
            ExternalDisplayIdentifier = string.IsNullOrWhiteSpace(username) ? email : username,
            Claims = principal.Claims
                .Select(c => new ExternalAuthenticationClaim(c.Type, c.Value))
                .ToList()
        };

        // 6. Delegate. nop handles link-by-email / auto-register / sign-in / cart migration /
        //    CustomerLoggedinEvent / activity log internally and returns the redirect IActionResult.
        var result = await _externalAuthenticationService.AuthenticateAsync(parameters, returnUrl);

        // 7. Group → role mapping (M3). Before the audit row so the Notes can record which
        //    roles were granted. Only runs when the customer was successfully signed in and
        //    RefreshRolesOnLogin is on.
        var customerAfter = await _customerService.GetCustomerByEmailAsync(email);
        IList<int> grantedRoleIds = Array.Empty<int>();
        IList<int> removedRoleIds = Array.Empty<int>();

        if (customerAfter != null && settings.RefreshRolesOnLogin)
        {
            (grantedRoleIds, removedRoleIds) = await SyncCustomerRolesAsync(customerAfter, store.Id, groups, settings);
        }

        // 8. Audit AFTER the side effect — Linked vs AutoRegistered determined by whether
        //    the email already mapped to a customer before the call.
        if (customerAfter != null)
        {
            var auditType = customerBefore == null
                ? SsoAuthEventTypes.AutoRegistered
                : SsoAuthEventTypes.Linked;

            var notes = BuildAuditNotes(groups, grantedRoleIds, removedRoleIds);

            await _eventService.LogAsync(
                eventType: auditType,
                protocol: protocolName,
                storeId: store.Id,
                customerId: customerAfter.Id,
                customerEmail: email,
                idpSubject: subject,
                notes: notes);
        }

        return result;
    }

    #endregion

    #region Role sync (M3)

    /// <summary>
    /// Reconciles the customer's role membership against the plugin's "owned" set for the
    /// store. The owned set is the union of every role appearing in any active mapping
    /// plus the configured <c>DefaultRoleSystemName</c>. After this call:
    /// <list type="bullet">
    ///   <item>roles in (owned set ∩ resolved set) — customer is in;</item>
    ///   <item>roles in (owned set \ resolved set) — customer is out;</item>
    ///   <item>roles outside the owned set — left untouched.</item>
    /// </list>
    /// Privileged roles are never in the owned set (the resolver filters them at the data
    /// layer regardless of mapping configuration).
    /// </summary>
    private async Task<(IList<int> granted, IList<int> removed)> SyncCustomerRolesAsync(
        Nop.Core.Domain.Customers.Customer customer,
        int storeId,
        IList<string> idpGroupIds,
        SsoConfigurationSettings settings)
    {
        // Compute the resolved set = role IDs from group mappings + default role (if set).
        var resolved = (await _groupRoleMappingService.ResolveCustomerRoleIdsAsync(storeId, idpGroupIds)).ToHashSet();

        if (!string.IsNullOrWhiteSpace(settings.DefaultRoleSystemName))
        {
            var defaultRole = await _customerService.GetCustomerRoleBySystemNameAsync(settings.DefaultRoleSystemName);
            if (defaultRole is not null && !PrivilegedRoleSystemNames.Contains(defaultRole.SystemName))
                resolved.Add(defaultRole.Id);
        }

        // The plugin's "owned" set this store gets to manage on each login.
        var owned = (await _groupRoleMappingService.GetOwnedRoleIdsForStoreAsync(storeId)).ToHashSet();
        if (!string.IsNullOrWhiteSpace(settings.DefaultRoleSystemName))
        {
            var defaultRole = await _customerService.GetCustomerRoleBySystemNameAsync(settings.DefaultRoleSystemName);
            if (defaultRole is not null)
                owned.Add(defaultRole.Id);
        }

        if (owned.Count == 0)
            return (Array.Empty<int>(), Array.Empty<int>()); // nothing to manage

        // Customer's current role membership.
        var currentRoles = await _customerService.GetCustomerRolesAsync(customer);
        var currentRoleIds = currentRoles.Select(r => r.Id).ToHashSet();

        // Add (resolved ∩ owned) \ current.
        var toAdd = resolved.Intersect(owned).Except(currentRoleIds).ToList();
        // Remove (owned \ resolved) ∩ current.
        var toRemove = owned.Except(resolved).Intersect(currentRoleIds).ToList();

        foreach (var roleId in toAdd)
        {
            var role = await _customerService.GetCustomerRoleByIdAsync(roleId);
            if (role is null) continue;
            await _customerService.AddCustomerRoleMappingAsync(new Nop.Core.Domain.Customers.CustomerCustomerRoleMapping
            {
                CustomerId = customer.Id,
                CustomerRoleId = role.Id
            });
        }

        foreach (var roleId in toRemove)
        {
            var mapping = currentRoles.FirstOrDefault(r => r.Id == roleId);
            if (mapping is null) continue;
            // RemoveCustomerRoleMapping wants the join row, not the role itself; nop exposes
            // it via the CustomerService. We use the customer + role overload nop ships.
            await _customerService.RemoveCustomerRoleMappingAsync(customer, mapping);
        }

        return (toAdd, toRemove);
    }

    /// <summary>
    /// Build the Notes string for the post-auth audit row. Captures groups asserted by the
    /// IdP plus any role-grant/revoke deltas applied this login. Keeps the column human-readable.
    /// </summary>
    private static string? BuildAuditNotes(IList<string> groups, IList<int> granted, IList<int> removed)
    {
        if (groups.Count == 0 && granted.Count == 0 && removed.Count == 0)
            return null;

        var parts = new List<string>();
        if (groups.Count > 0) parts.Add($"Groups: {string.Join(",", groups)}");
        if (granted.Count > 0) parts.Add($"Granted role ids: {string.Join(",", granted)}");
        if (removed.Count > 0) parts.Add($"Revoked role ids: {string.Join(",", removed)}");
        return string.Join(" | ", parts);
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Returns the first non-empty claim value for <paramref name="primaryType"/>;
    /// falls back to <paramref name="fallbackType"/> when the primary isn't present.
    /// </summary>
    private static string FindFirst(ClaimsPrincipal principal, string primaryType, string fallbackType)
    {
        var value = string.IsNullOrWhiteSpace(primaryType)
            ? null
            : principal.FindFirstValue(primaryType);

        if (string.IsNullOrWhiteSpace(value))
            value = principal.FindFirstValue(fallbackType);

        return value ?? string.Empty;
    }

    /// <summary>
    /// Email-domain allowlist matcher. Empty CSV means all domains accepted. Supports
    /// literal domains (<c>example.com</c>) and wildcard subdomain suffixes (<c>*.example.com</c>).
    /// </summary>
    private static bool IsEmailDomainAllowed(string email, string allowedDomainsCsv)
    {
        if (string.IsNullOrWhiteSpace(allowedDomainsCsv))
            return true;

        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1)
            return false;

        var emailDomain = email[(atIndex + 1)..].ToLowerInvariant();

        foreach (var raw in allowedDomainsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pattern = raw.ToLowerInvariant();

            if (pattern.StartsWith("*."))
            {
                var suffix = pattern[1..]; // includes the leading dot, e.g. ".example.com"
                var bareDomain = suffix.TrimStart('.');
                if (emailDomain == bareDomain || emailDomain.EndsWith(suffix))
                    return true;
            }
            else if (emailDomain == pattern)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if the customer is in any of the privileged roles that
    /// must never be linked to or auto-assigned via SSO.
    /// </summary>
    private async Task<bool> IsPrivilegedAsync(Nop.Core.Domain.Customers.Customer customer)
    {
        var roles = await _customerService.GetCustomerRolesAsync(customer);
        return roles.Any(r => PrivilegedRoleSystemNames.Contains(r.SystemName, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Redirect to nop's standard login route with the original <paramref name="returnUrl"/>.
    /// </summary>
    private static IActionResult RedirectToLogin(string? returnUrl)
    {
        return new RedirectToRouteResult(NopRouteNames.General.LOGIN, new { returnUrl });
    }

    #endregion
}
