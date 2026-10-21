using FluentValidation;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.ExternalAuth.SSO.Validators;

/// <summary>
/// FluentValidation rules for the Add SSO account picker.
/// </summary>
/// <remarks>
/// The store must be one the picker offers: a real store without an SSO configuration yet. A tampered
/// post naming another store (or none) is refused here, before the controller changes the admin's
/// SSO store scope.
/// </remarks>
public class AddSsoAccountModelValidator : BaseNopValidator<AddSsoAccountModel>
{
    public AddSsoAccountModelValidator(
        ILocalizationService localizationService,
        ISsoStoreSummaryService storeSummaryService)
    {
        RuleFor(m => m.StoreId)
            .MustAsync(async (storeId, _) =>
                (await storeSummaryService.ListUnconfiguredAsync()).Any(s => s.StoreId == storeId))
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.SsoAccounts.Add.StoreInvalid"));
    }
}
