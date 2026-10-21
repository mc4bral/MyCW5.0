using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Services.Helpers;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Default implementation of <see cref="ISamlMetadataBuilder"/>.
/// </summary>
public class SamlMetadataBuilder : ISamlMetadataBuilder
{
    /// <summary>How long IdPs should consider the metadata valid before refetching.</summary>
    private const int MetadataValidDays = 365;

    #region Fields

    protected readonly ISaml2ConfigurationFactory _configFactory;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public SamlMetadataBuilder(
        ISaml2ConfigurationFactory configFactory,
        ISettingService settingService,
        IStoreContext storeContext,
        IWebHelper webHelper)
    {
        _configFactory = configFactory;
        _settingService = settingService;
        _storeContext = storeContext;
        _webHelper = webHelper;
    }

    #endregion

    #region Utilities

    private string ResolveAcsUrl(SsoConfigurationSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.AcsUrl))
            return settings.AcsUrl;

        var baseUrl = !string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? settings.BaseUrl.TrimEnd('/')
            : _webHelper.GetStoreLocation().TrimEnd('/');

        return baseUrl + SSOAuthenticationDefaults.SamlAcsPath;
    }

    private Uri? ResolveSpLogoutUrl(SsoConfigurationSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.SpLogoutUrl)
            && Uri.TryCreate(settings.SpLogoutUrl, UriKind.Absolute, out var explicitUrl))
            return explicitUrl;

        // Fall back to base + /saml/logout only when SAML is generally configured. We don't
        // synthesise a logout URL if the admin hasn't configured the IdP SLO URL either —
        // there's no point advertising SLO if the IdP can't initiate it.
        if (string.IsNullOrWhiteSpace(settings.IdpSloUrl))
            return null;

        var baseUrl = !string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? settings.BaseUrl.TrimEnd('/')
            : _webHelper.GetStoreLocation().TrimEnd('/');

        return new Uri(baseUrl + SSOAuthenticationDefaults.SamlLogoutPath);
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IActionResult> BuildMetadataAsync()
    {
        var config = await _configFactory.CreateForCurrentStoreAsync(SamlConfigPurpose.Metadata);
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);

        var acsUrl = new Uri(ResolveAcsUrl(settings));
        var spLogoutUrl = ResolveSpLogoutUrl(settings);

        var entityDescriptor = new EntityDescriptor(config)
        {
            ValidUntil = MetadataValidDays
        };

        var spDescriptor = new SPSsoDescriptor
        {
            AuthnRequestsSigned = settings.SignedRequest,
            WantAssertionsSigned = true,
            NameIDFormats = new[] { NameIdentifierFormats.Persistent },
            AssertionConsumerServices = new[]
            {
                new AssertionConsumerService
                {
                    //Index = 0,
                    IsDefault = true,
                    Binding = ProtocolBindings.HttpPost,
                    Location = acsUrl
                }
            }
        };

        // SLO advertised only when an SP logout URL is configured. IdPs will only invoke
        // SLO if it appears in the metadata — keeping it conditional avoids advertising a
        // logout endpoint nobody configured.
        if (spLogoutUrl is not null)
        {
            spDescriptor.SingleLogoutServices = new[]
            {
                new SingleLogoutService
                {
                    Binding = settings.LogoutBinding == SamlBinding.HttpPost
                        ? ProtocolBindings.HttpPost
                        : ProtocolBindings.HttpRedirect,
                    Location = spLogoutUrl
                }
            };
        }

        // Advertise the SP's signing certificate when AuthnRequest or logout signing is on, so the
        // IdP can verify our requests. Same for encryption when EncryptedAssertion is on.
        if (config.SigningCertificate is not null)
            spDescriptor.SigningCertificates = new[] { config.SigningCertificate };

        if (settings.EncryptedAssertion && config.DecryptionCertificates.Count > 0)
            spDescriptor.EncryptionCertificates = new[] { config.DecryptionCertificates[0] };

        entityDescriptor.SPSsoDescriptor = spDescriptor;

        return new Saml2Metadata(entityDescriptor).CreateMetadata().ToActionResult();
    }

    #endregion
}
