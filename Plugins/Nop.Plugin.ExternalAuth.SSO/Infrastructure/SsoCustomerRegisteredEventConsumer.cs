using System.Security.Claims;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Authentication.External;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Events;

namespace Nop.Plugin.ExternalAuth.SSO.Infrastructure;

/// <summary>
/// Copies the IdP's first and last name onto a customer nop registers from an SSO sign-in.
/// </summary>
/// <remarks>
/// nop's <c>ExternalAuthenticationParameters</c> have no name fields, so registration leaves them
/// blank and publishes <see cref="CustomerAutoRegisteredByExternalMethodEvent"/> for a plugin to
/// fill in — the same pattern as nop's own Facebook plugin. Names are read through the store's
/// configured claim types, falling back to the standard <see cref="ClaimTypes"/> URIs. The event
/// fires for every new SSO customer, including one nop holds for e-mail validation or approval.
/// nop discovers consumers by type; no registration needed.
/// </remarks>
public class SsoCustomerRegisteredEventConsumer : IConsumer<CustomerAutoRegisteredByExternalMethodEvent>
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public SsoCustomerRegisteredEventConsumer(
        ICustomerService customerService,
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _customerService = customerService;
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Utilities

    private static string? FindClaim(IList<ExternalAuthenticationClaim>? claims, string? primaryType, string fallbackType)
    {
        if (claims is null)
            return null;

        var value = string.IsNullOrWhiteSpace(primaryType)
            ? null
            : claims.FirstOrDefault(c => c.Type == primaryType)?.Value;

        if (string.IsNullOrWhiteSpace(value))
            value = claims.FirstOrDefault(c => c.Type == fallbackType)?.Value;

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Handle the event.
    /// </summary>
    /// <param name="eventMessage">The customer nop just registered and the parameters it came from</param>
    public async Task HandleEventAsync(CustomerAutoRegisteredByExternalMethodEvent eventMessage)
    {
        var customer = eventMessage?.Customer;
        var parameters = eventMessage?.AuthenticationParameters;

        if (customer is null || parameters is null
            || !string.Equals(parameters.ProviderSystemName, SSOAuthenticationDefaults.SystemName, StringComparison.Ordinal))
            return;

        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);

        var firstName = FindClaim(parameters.Claims, settings.FirstNameClaimType, ClaimTypes.GivenName);
        var lastName = FindClaim(parameters.Claims, settings.LastNameClaimType, ClaimTypes.Surname);

        var changed = false;
        if (string.IsNullOrWhiteSpace(customer.FirstName) && firstName is not null)
        {
            customer.FirstName = firstName;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(customer.LastName) && lastName is not null)
        {
            customer.LastName = lastName;
            changed = true;
        }

        if (changed)
            await _customerService.UpdateCustomerAsync(customer);
    }

    #endregion
}
