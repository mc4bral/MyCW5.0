using FluentValidation;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Stores;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.ExternalAuth.SSO.Validators;

/// <summary>
/// FluentValidation rules for the admin Configure form. Fixes legacy P2-16
/// (no validation on required fields).
/// </summary>
/// <remarks>
/// Auto-discovered by nopCommerce's FluentValidation registration (the type is in the
/// plugin's assembly which is scanned at startup).
///
/// Required-when-enabled rules per protocol selection, plus URL well-formedness
/// for URL fields, and a non-negative audit retention. Group-mapping values are validated by
/// <see cref="SsoGroupRoleMappingModelValidator"/>; claim types and email domains are free text.
/// </remarks>
public class ConfigurationModelValidator : BaseNopValidator<ConfigurationModel>
{
    public ConfigurationModelValidator(
        ILocalizationService localizationService,
        ISettingService settingService,
        IStoreService storeService)
    {
        // D10 — never save a state that locks every admin out of a store's own login: "Redirect
        // login to SSO" on with no bypass token (the bypass link is then off, and SSO refuses admin
        // accounts). Checked from both sides of the form:
        // 1. A store-scope save turning the redirect on while no token is stored. (The system-wide
        //    card is not on that page, so the token comes from the stored settings.)
        RuleFor(m => m.RedirectLoginToSso)
            .MustAsync(async (m, _, _) =>
            {
                var systemWide = await settingService.LoadSettingAsync<SsoSystemSettings>(storeId: 0);
                return !SsoMonitoringService.LocksAdminsOut(m.IsEnabled, m.Protocol, m.RedirectLoginToSso,
                    hasBypassToken: !string.IsNullOrEmpty(systemWide.SsoBypassToken));
            })
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.RedirectLoginToSso.NeedsBypassToken"))
            .When(m => m.RedirectLoginToSso && m.IsEnabled && m.Protocol != SsoProtocol.Off);

        // 2. An "All stores" save clearing the token while a store redirects login to SSO.
        RuleFor(m => m.ClearSsoBypassToken)
            .MustAsync(async (_, _) =>
            {
                foreach (var store in await storeService.GetAllStoresAsync())
                {
                    var s = await settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
                    if (SsoMonitoringService.LocksAdminsOut(s.IsEnabled, s.Protocol, s.RedirectLoginToSso, hasBypassToken: false))
                        return false;
                }

                return true;
            })
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.SsoBypassToken.NeededForRedirect"))
            .When(m => m.ClearSsoBypassToken);

        // BaseUrl is required when the plugin is enabled (the SAML SP endpoints and OIDC
        // callback are constructed off it).
        RuleFor(m => m.AuditRetentionDays)
            .GreaterThanOrEqualTo(0)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.AuditRetentionDays.Invalid"));

        // System-wide card only. A store-scope post does not carry the field (it binds to 0), and the
        // form does not post the scope either — so 0 means "not on this page" and is not validated;
        // the factory then keeps the stored value.
        RuleFor(m => m.CertificateWarningDays)
            .InclusiveBetween(1, 365)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.CertificateWarningDays.Invalid"))
            .When(m => m.CertificateWarningDays != 0);

        RuleFor(m => m.BaseUrl)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.BaseUrl.Required"))
            .When(m => m.IsEnabled && m.Protocol != SsoProtocol.Off);

        RuleFor(m => m.BaseUrl)
            .Must(BeAValidAbsoluteUrlOrEmpty)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.BaseUrl.Invalid"));

        // OIDC required fields.
        RuleFor(m => m.Authority)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.Authority.Required"))
            .When(m => m.IsEnabled && m.Protocol == SsoProtocol.OpenIdConnect);

        RuleFor(m => m.Authority)
            .Must(BeAValidAbsoluteUrlOrEmpty)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.Authority.Invalid"));

        RuleFor(m => m.ClientId)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.ClientId.Required"))
            .When(m => m.IsEnabled && m.Protocol == SsoProtocol.OpenIdConnect);

        // The ClientSecret check considers the stored-secret indicator: a model may post
        // empty ClientSecret + HasClientSecret=true (preserves existing). It is only
        // missing when neither side carries a value.
        RuleFor(m => m.ClientSecret)
            .Must((m, _) => !string.IsNullOrWhiteSpace(m.ClientSecret) || m.HasClientSecret)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.ClientSecret.Required"))
            .When(m => m.IsEnabled && m.Protocol == SsoProtocol.OpenIdConnect);

        // SAML required fields.
        RuleFor(m => m.IdpEntityId)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.IdpEntityId.Required"))
            .When(m => m.IsEnabled && m.Protocol == SsoProtocol.Saml2);

        RuleFor(m => m.IdpSsoUrl)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.IdpSsoUrl.Required"))
            .When(m => m.IsEnabled && m.Protocol == SsoProtocol.Saml2);

        RuleFor(m => m.IdpSsoUrl)
            .Must(BeAValidAbsoluteUrlOrEmpty)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.IdpSsoUrl.Invalid"));

        RuleFor(m => m.IdpSloUrl)
            .Must(BeAValidAbsoluteUrlOrEmpty)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.IdpSloUrl.Invalid"));

        // Either the admin uploads a cert this turn or one is already stored.
        RuleFor(m => m.IdpCertificateBase64)
            .Must((m, _) => !string.IsNullOrWhiteSpace(m.IdpCertificateBase64) || m.HasIdpCertificate)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.IdpCertificateBase64.Required"))
            .When(m => m.IsEnabled && m.Protocol == SsoProtocol.Saml2);

        // Optional URL fields — well-formed when populated.
        RuleFor(m => m.IdpMetadataUrl).Must(BeAValidAbsoluteUrlOrEmpty)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.IdpMetadataUrl.Invalid"));
        RuleFor(m => m.AcsUrl).Must(BeAValidAbsoluteUrlOrEmpty)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.AcsUrl.Invalid"));
        RuleFor(m => m.SpLogoutUrl).Must(BeAValidAbsoluteUrlOrEmpty)
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Fields.SpLogoutUrl.Invalid"));
    }

    private static bool BeAValidAbsoluteUrlOrEmpty(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
