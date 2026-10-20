using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Core.Http;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Authentication.External;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Helpers;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="ISsoUserProvisioningService"/>.
/// </summary>
public class SsoUserProvisioningService : ISsoUserProvisioningService
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly IExternalAuthenticationService _externalAuthenticationService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly ISettingService _settingService;
    protected readonly ISsoEventService _eventService;
    protected readonly ISsoGroupRoleMappingService _groupRoleMappingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWebHelper _webHelper;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public SsoUserProvisioningService(
        ICustomerService customerService,
        IExternalAuthenticationService externalAuthenticationService,
        IGenericAttributeService genericAttributeService,
        ISettingService settingService,
        ISsoEventService eventService,
        ISsoGroupRoleMappingService groupRoleMappingService,
        IStoreContext storeContext,
        IWebHelper webHelper,
        IWorkContext workContext)
    {
        _customerService = customerService;
        _externalAuthenticationService = externalAuthenticationService;
        _genericAttributeService = genericAttributeService;
        _settingService = settingService;
        _eventService = eventService;
        _groupRoleMappingService = groupRoleMappingService;
        _storeContext = storeContext;
        _webHelper = webHelper;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    #region Claims

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
    /// Whether the IdP replaced the group list with an overage marker. Entra does this when a
    /// user is in more groups than fit in the token (200 for a JWT, 150 for a SAML assertion):
    /// OIDC carries <c>_claim_names</c> pointing <c>groups</c> at Microsoft Graph (or
    /// <c>hasgroups</c> in the implicit flow), SAML carries a <c>groups.link</c> claim.
    /// </summary>
    private static bool HasGroupsOverage(ClaimsPrincipal principal)
    {
        foreach (var claim in principal.Claims)
        {
            if (claim.Type.Equals(SSOAuthenticationDefaults.OidcGroupsOverageClaimType, StringComparison.OrdinalIgnoreCase)
                && claim.Value.Contains("\"groups\"", StringComparison.OrdinalIgnoreCase))
                return true;

            if (claim.Type.Equals(SSOAuthenticationDefaults.OidcHasGroupsClaimType, StringComparison.OrdinalIgnoreCase)
                && bool.TryParse(claim.Value, out var hasGroups) && hasGroups)
                return true;

            if (claim.Type.Equals(SSOAuthenticationDefaults.SamlGroupsOverageClaimType, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

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

    #endregion

    #region Customers

    private async Task<bool> IsPrivilegedAsync(Customer customer)
    {
        var roles = await _customerService.GetCustomerRolesAsync(customer);
        return roles.Any(r => SsoRoles.IsPrivileged(r.SystemName));
    }

    /// <summary>
    /// The customer nop signed in during this request, when it is the one the IdP asserted.
    /// nop's sign-in sets the work context's customer; a guest or another customer means the
    /// sign-in did not happen (error, pending e-mail validation or admin approval).
    /// </summary>
    private async Task<Customer?> GetSignedInCustomerAsync(string email)
    {
        var current = await _workContext.GetCurrentCustomerAsync();

        if (current is null || !await _customerService.IsRegisteredAsync(current))
            return null;

        return string.Equals(current.Email, email, StringComparison.OrdinalIgnoreCase) ? current : null;
    }

    private IActionResult RedirectToLogin(string? returnUrl)
        => new RedirectToRouteResult(NopRouteNames.General.LOGIN, new { returnUrl });

    #endregion

    #region Roles

    /// <summary>
    /// Ids of the store's default roles that still exist and are not privileged.
    /// </summary>
    private async Task<IList<int>> GetDefaultRoleIdsAsync(IEnumerable<int>? defaultRoleIds)
    {
        var result = new List<int>();

        foreach (var roleId in (defaultRoleIds ?? []).Distinct())
        {
            var role = await _customerService.GetCustomerRoleByIdAsync(roleId);
            if (role is not null && !SsoRoles.IsPrivileged(role.SystemName))
                result.Add(role.Id);
        }

        return result;
    }

    private async Task<(IList<int> granted, IList<int> removed)> SyncCustomerRolesAsync(
        Customer customer,
        int storeId,
        IList<string> idpGroupIds,
        SsoConfigurationSettings settings)
    {
        var defaultRoleIds = await GetDefaultRoleIdsAsync(settings.DefaultRoleIds);

        // Resolved = roles from group mappings + the default roles.
        var resolved = (await _groupRoleMappingService.ResolveCustomerRoleIdsAsync(storeId, idpGroupIds)).ToHashSet();
        resolved.UnionWith(defaultRoleIds);

        // Owned = every role this store's SSO manages: any mapped role + the default roles.
        var owned = (await _groupRoleMappingService.GetOwnedRoleIdsForStoreAsync(storeId)).ToHashSet();
        owned.UnionWith(defaultRoleIds);

        if (owned.Count == 0)
            return (Array.Empty<int>(), Array.Empty<int>());

        var currentRoles = await _customerService.GetCustomerRolesAsync(customer);
        var currentRoleIds = currentRoles.Select(r => r.Id).ToHashSet();

        var toAdd = resolved.Intersect(owned).Except(currentRoleIds).ToList();
        var toRemove = owned.Except(resolved).Intersect(currentRoleIds).ToList();

        foreach (var roleId in toAdd)
        {
            await _customerService.AddCustomerRoleMappingAsync(new CustomerCustomerRoleMapping
            {
                CustomerId = customer.Id,
                CustomerRoleId = roleId
            });
        }

        foreach (var role in currentRoles.Where(r => toRemove.Contains(r.Id)))
            await _customerService.RemoveCustomerRoleMappingAsync(customer, role);

        return (toAdd, toRemove);
    }

    private static string? BuildAuditNotes(string? lead, IList<string> groups, IList<int> granted, IList<int> removed)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(lead)) parts.Add(lead);
        if (groups.Count > 0) parts.Add($"Groups/app roles: {string.Join(",", groups)}");
        if (granted.Count > 0) parts.Add($"Granted role ids: {string.Join(",", granted)}");
        if (removed.Count > 0) parts.Add($"Revoked role ids: {string.Join(",", removed)}");

        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    #endregion

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IActionResult> HandleAsync(
        ClaimsPrincipal principal,
        string protocolName,
        string? accessToken,
        string? returnUrl,
        string? idToken = null)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);

        // No returnUrl from the login → the store's post-login page, when it is a local URL.
        if (string.IsNullOrWhiteSpace(returnUrl)
            && !string.IsNullOrWhiteSpace(settings.PostLoginRedirectUrl)
            && _webHelper.CheckIsLocalUrl(settings.PostLoginRedirectUrl.Trim()))
        {
            returnUrl = settings.PostLoginRedirectUrl.Trim();
        }

        // 1. Claims, through the store's claim types with the standard URIs as fallback. Names
        //    are not read here: nop passes all claims on, and SsoCustomerRegisteredEventConsumer
        //    copies them onto a customer nop registers.
        var email = FindFirst(principal, settings.EmailClaimType, ClaimTypes.Email).Trim();
        var username = FindFirst(principal, settings.UsernameClaimType, ClaimTypes.Name);

        // The link between the nop customer and the IdP account is keyed on this value; an
        // empty one would link every subject-less login to the same account.
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
            subject = email;

        // Values the group → role mappings match: group ids and app roles alike.
        var groups = principal.FindAll(c => c.Type == settings.GroupsClaimType
                                            || !string.IsNullOrEmpty(settings.RolesClaimType) && c.Type == settings.RolesClaimType)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        async Task<IActionResult> FailAsync(int? customerId, string reason, string detail)
        {
            await _eventService.LogFailureAsync(protocolName, reason, detail, customerId, email, subject);
            return RedirectToLogin(returnUrl);
        }

        // 2. Usable email.
        if (string.IsNullOrWhiteSpace(email))
            return await FailAsync(null, SsoFailureReasons.AcsNoEmailClaim, "No email claim present in the IdP token.");

        // 3. Email-domain allowlist.
        if (!IsEmailDomainAllowed(email, settings.AllowedEmailDomains))
            return await FailAsync(null, SsoFailureReasons.AcsEmailDomainBlocked,
                $"Email domain rejected by allowlist '{settings.AllowedEmailDomains}'.");

        // 4. Admin accounts can never be signed in through SSO.
        var existingCustomer = await _customerService.GetCustomerByEmailAsync(email);
        if (existingCustomer != null && await IsPrivilegedAsync(existingCustomer))
            return await FailAsync(existingCustomer.Id, SsoFailureReasons.AcsAdminAccountBlocked,
                "Existing customer is in a privileged role — SSO link rejected.");

        // 5. New customers only when the store allows it. nop's own registration type
        //    (CustomerSettings.UserRegistrationType) still applies on top of this.
        if (existingCustomer is null && !settings.AutoRegister)
            return await FailAsync(null, SsoFailureReasons.AcsAutoRegisterDisabled,
                "No customer with this email and auto-registration is off for the store.");

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

        // 6. Link an existing customer by email. nop only signs in customers already linked to
        //    this IdP account; for an unlinked one it tries to register and stops with "email
        //    already exists". The admin block above has already run.
        var linkedNow = false;
        if (existingCustomer is not null
            && await _externalAuthenticationService.GetUserByExternalAuthenticationParametersAsync(parameters) is null)
        {
            await _externalAuthenticationService.AssociateExternalAccountWithUserAsync(existingCustomer, parameters);
            linkedNow = true;
        }

        // 7. nop signs in (or registers), migrates the cart, publishes its events and returns
        //    the redirect.
        var result = await _externalAuthenticationService.AuthenticateAsync(parameters, returnUrl);

        // 8. Act only on a sign-in that actually happened.
        var signedIn = await GetSignedInCustomerAsync(email);
        if (signedIn is null)
        {
            var registered = existingCustomer is null ? await _customerService.GetCustomerByEmailAsync(email) : null;
            if (registered is not null)
            {
                // Created, but nop holds it for e-mail validation or admin approval. (Names were
                // set by SsoCustomerRegisteredEventConsumer when nop registered the customer.)
                await _eventService.LogAsync(
                    eventType: SsoAuthEventTypes.AutoRegistered,
                    protocol: protocolName,
                    customerId: registered.Id,
                    customerEmail: email,
                    idpSubject: subject,
                    notes: "Registered, not signed in: nop requires e-mail validation or admin approval.");

                return result;
            }

            await _eventService.LogFailureAsync(protocolName, SsoFailureReasons.AcsCustomerLoginFailed,
                "nop did not sign the customer in (see the message on the login page).",
                existingCustomer?.Id, email, subject);

            return result;
        }

        // 9. Remember how the customer signed in, and the OIDC id_token (sent back as
        //    id_token_hint), for federated logout. A SAML sign-in clears any old id_token.
        await _genericAttributeService.SaveAttributeAsync(signedIn, SsoCustomerAttributes.SignInProtocol, protocolName);
        await _genericAttributeService.SaveAttributeAsync<string?>(signedIn, SsoCustomerAttributes.OidcIdToken,
            string.IsNullOrEmpty(idToken) ? null : idToken);

        // 10. Group → role mapping within the store's owned role set. With a group overage the
        //     token carries no group list, and syncing against an empty list would revoke every
        //     owned role — so the customer's roles are left exactly as they are.
        IList<int> grantedRoleIds = Array.Empty<int>();
        IList<int> removedRoleIds = Array.Empty<int>();
        var groupsOverage = HasGroupsOverage(principal);
        if (settings.RefreshRolesOnLogin && !groupsOverage)
            (grantedRoleIds, removedRoleIds) = await SyncCustomerRolesAsync(signedIn, store.Id, groups, settings);

        // 11. Audit after the side effects.
        var (auditType, lead) = existingCustomer is null
            ? (SsoAuthEventTypes.AutoRegistered, (string?)null)
            : linkedNow
                ? (SsoAuthEventTypes.Linked, "Existing customer linked by email.")
                : (SsoAuthEventTypes.SignedIn, (string?)null);

        if (groupsOverage && settings.RefreshRolesOnLogin)
        {
            var overageNote = SsoFailureReasons.Format(SsoFailureReasons.GroupsOverage,
                "The IdP sent a group-overage marker instead of the group list (the user is in too many groups); role sync skipped, no roles changed.");
            lead = string.IsNullOrEmpty(lead) ? overageNote : $"{lead} | {overageNote}";
        }

        await _eventService.LogAsync(
            eventType: auditType,
            protocol: protocolName,
            customerId: signedIn.Id,
            customerEmail: email,
            idpSubject: subject,
            notes: BuildAuditNotes(lead, groups, grantedRoleIds, removedRoleIds));

        return result;
    }

    #endregion
}
