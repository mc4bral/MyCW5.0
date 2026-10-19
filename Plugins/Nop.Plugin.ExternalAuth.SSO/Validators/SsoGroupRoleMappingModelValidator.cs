using FluentValidation;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.ExternalAuth.SSO.Validators;

/// <summary>
/// FluentValidation rules for the Group → Role Mapping edit form (M3).
/// </summary>
/// <remarks>
/// Auto-discovered by nopCommerce's validator registration. The privileged-role guard
/// runs here too — UI defence on top of the service-layer guard, so an admin can't even
/// post a privileged role ID through a tampered form. Entra group IDs (Object IDs) are
/// 36-char GUIDs; we accept the canonical form with hyphens.
/// </remarks>
public class SsoGroupRoleMappingModelValidator : BaseNopValidator<SsoGroupRoleMappingModel>
{
    /// <summary>GUID regex matching Entra Object ID shape.</summary>
    private const string EntraGuidPattern = @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

    public SsoGroupRoleMappingModelValidator(
        ICustomerService customerService,
        ILocalizationService localizationService)
    {
        RuleFor(m => m.IdpGroupId)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Required"));

        RuleFor(m => m.IdpGroupId)
            .Matches(EntraGuidPattern)
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
                return !string.Equals(role.SystemName, "Administrators", StringComparison.OrdinalIgnoreCase);
            })
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole.Privileged"))
            .When(m => m.CustomerRoleId > 0);
    }
}
