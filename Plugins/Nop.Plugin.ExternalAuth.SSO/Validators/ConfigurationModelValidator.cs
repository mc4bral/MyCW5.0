using FluentValidation;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Localization;
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
/// M2.b scope: required-when-enabled rules per protocol selection, plus URL well-formedness
/// for URL fields. Stricter rules (Entra Object ID format on group mappings, claim type
/// allowlists, email-domain wildcard syntax) land in later milestones alongside the
/// features that depend on them.
/// </remarks>
public class ConfigurationModelValidator : BaseNopValidator<ConfigurationModel>
{
    public ConfigurationModelValidator(ILocalizationService localizationService)
    {
        // BaseUrl is required when the plugin is enabled (the SAML SP endpoints and OIDC
        // callback are constructed off it).
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
