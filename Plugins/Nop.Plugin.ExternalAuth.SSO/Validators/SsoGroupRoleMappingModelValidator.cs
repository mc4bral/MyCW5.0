using FluentValidation;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.ExternalAuth.SSO.Validators;

/// <summary>
/// FluentValidation rules for the Group → Role Mapping edit form.
/// </summary>
/// <remarks>
/// Auto-discovered by nopCommerce's validator registration. The privileged-role guard
/// runs here too — UI defence on top of the service-layer guard, so an admin can't even
/// post a privileged role ID through a tampered form. The IdP value is a group Object ID or an
/// app-role value (see <c>IdpValuePattern</c>).
/// </remarks>
public class SsoGroupRoleMappingModelValidator : BaseNopValidator<SsoGroupRoleMappingModel>
{
    /// <summary>
    /// An IdP value a mapping can match: an Entra group Object ID (a GUID) or an app-role value
    /// (e.g. <c>Store.Manager</c>). Letters, digits and <c>. _ - : /</c>, no spaces, at most 100
    /// characters (the column width).
    /// </summary>
    internal const string IdpValuePattern = @"^[A-Za-z0-9][A-Za-z0-9._:/\-]{0,99}$";

    public SsoGroupRoleMappingModelValidator(
        ICustomerService customerService,
        ILocalizationService localizationService)
    {
        RuleFor(m => m.IdpGroupId)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Required"));

        RuleFor(m => m.IdpGroupId)
            .Matches(IdpValuePattern)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Invalid"))
            .When(m => !string.IsNullOrEmpty(m.IdpGroupId));

        RuleFor(m => m.IdpGroupDisplayName)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupDisplayName.Required"));

        RuleFor(m => m.CustomerRoleId)
            .GreaterThan(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole.Required"));

        // Privileged-role guard at the validation layer. Independent of the dropdown
        // filtering done by the factory — defends against form-tampering.
        RuleFor(m => m.CustomerRoleId)
            .MustAsync(async (id, _) =>
            {
                if (id <= 0) return true; // separate rule handles the empty case
                var role = await customerService.GetCustomerRoleByIdAsync(id);
                if (role is null) return false;
                return !SsoRoles.IsPrivileged(role.SystemName);
            })
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole.Privileged"))
            .When(m => m.CustomerRoleId > 0);
    }
}
