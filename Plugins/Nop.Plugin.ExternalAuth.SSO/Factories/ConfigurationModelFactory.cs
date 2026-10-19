using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;

namespace Nop.Plugin.ExternalAuth.SSO.Factories;

/// <summary>
/// Default implementation of <see cref="IConfigurationModelFactory"/>.
/// </summary>
public class ConfigurationModelFactory : IConfigurationModelFactory
{
    #region Fields

    private readonly ISettingService _settingService;
    private readonly ISsoSecretsService _secretsService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IWorkContext _workContext;
    private readonly ICustomerService _customerService;
    #endregion

    #region Ctor

    public ConfigurationModelFactory(
        ISettingService settingService,
        ISsoSecretsService secretsService,
        IGenericAttributeService genericAttributeService,
        IWorkContext workContext,
        ICustomerService customerService)
    {
        _settingService = settingService;
        _secretsService = secretsService;
        _genericAttributeService = genericAttributeService;
        _workContext = workContext;
        _customerService = customerService;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<ConfigurationModel> PrepareConfigurationModelAsync()
    {
        int storeScope = await GetActiveStoreScopeSSOAsync();

        var perStore = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(storeScope);
        var systemWide = await _settingService.LoadSettingAsync<SsoSystemSettings>(storeId: 0);

        // Get all customer roles for the multi-select dropdown
        var allRoles = await _customerService.GetAllCustomerRolesAsync(showHidden: true);
        var availableRoles = allRoles.Select(role => new SelectListItem
        {
            Text = role.Name,
            Value = role.Id.ToString()
        }).ToList();

        // Parse the comma-separated role IDs from DefaultRoleSystemName
        var selectedRoleIds = new List<int>();
        if (!string.IsNullOrEmpty(perStore.DefaultRoleSystemName))
        {
            selectedRoleIds = perStore.DefaultRoleSystemName
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id.Trim(), out var roleId) ? roleId : 0)
                .Where(id => id > 0)
                .ToList();
        }

        var model = new ConfigurationModel
        {
            ActiveStoreScopeConfiguration = storeScope,

            Protocol = perStore.Protocol,
            IsEnabled = perStore.IsEnabled,
            BaseUrl = perStore.BaseUrl,
            DisplayOrder = perStore.DisplayOrder,
            IdpDisplayName = perStore.IdpDisplayName,
            EnableMockMode = perStore.EnableMockMode,

            Authority = perStore.Authority,
            ClientId = perStore.ClientId,
            ClientSecret = string.Empty,
            HasClientSecret = !string.IsNullOrEmpty(perStore.ClientSecret),
            CallbackPath = perStore.CallbackPath,
            Scopes = perStore.Scopes,

            IdpEntityId = perStore.IdpEntityId,
            IdpSsoUrl = perStore.IdpSsoUrl,
            IdpSloUrl = perStore.IdpSloUrl,
            IdpCertificateBase64 = string.Empty,
            HasIdpCertificate = !string.IsNullOrEmpty(perStore.IdpCertificateBase64),
            IdpMetadataUrl = perStore.IdpMetadataUrl,
            SpEntityId = perStore.SpEntityId,
            AcsUrl = perStore.AcsUrl,
            SpLogoutUrl = perStore.SpLogoutUrl,
            SpPfxBase64 = string.Empty,
            HasSpPfx = !string.IsNullOrEmpty(perStore.SpPfxBase64),
            SpPfxPassword = string.Empty,
            HasSpPfxPassword = !string.IsNullOrEmpty(perStore.SpPfxPassword),
            SignedRequest = perStore.SignedRequest,
            SignLogoutRequest = perStore.SignLogoutRequest,
            EncryptedAssertion = perStore.EncryptedAssertion,
            LoginBinding = perStore.LoginBinding,
            LogoutBinding = perStore.LogoutBinding,

            EmailClaimType = perStore.EmailClaimType,
            UsernameClaimType = perStore.UsernameClaimType,
            FirstNameClaimType = perStore.FirstNameClaimType,
            LastNameClaimType = perStore.LastNameClaimType,
            GroupsClaimType = perStore.GroupsClaimType,

            AutoRegister = perStore.AutoRegister,
            DefaultRoleSystemName = perStore.DefaultRoleSystemName,
            AvailableCustomerRoles = availableRoles,
            SelectedCustomerRoleIds = selectedRoleIds,
            AllowedEmailDomains = perStore.AllowedEmailDomains,
            RefreshRolesOnLogin = perStore.RefreshRolesOnLogin,
            RedirectLoginToSso = perStore.RedirectLoginToSso,

            PostLoginRedirectUrl = perStore.PostLoginRedirectUrl,
            PostLogoutRedirectUrl = perStore.PostLogoutRedirectUrl,

            CustomAttributesJson = perStore.CustomAttributesJson,

            IdpCertificatePreview = BuildSecretPreview(perStore.IdpCertificateBase64),
            SpPfxPreview = BuildSecretPreview(perStore.SpPfxBase64),

            SsoBypassToken = string.Empty,
            HasSsoBypassToken = !string.IsNullOrEmpty(systemWide.SsoBypassToken),
            LocalLoginAllowedRoles = systemWide.LocalLoginAllowedRoles,
            LoginAttemptCapPerHour = systemWide.LoginAttemptCapPerHour,
            EnableAuditLogging = systemWide.EnableAuditLogging
        };

        return model;
    }

    public async Task<Customer> GetCurrentCustomer()
    {
        return await _workContext.GetCurrentCustomerAsync();
    }

    public async Task ChangeSSOSope(int storeid)
    {
        await _genericAttributeService
            .SaveAttributeAsync(await _workContext.GetCurrentCustomerAsync(), "SSOStoreScopeConfigurationAttribute", storeid);
    }

    public async Task<int> GetActiveStoreScopeSSOAsync()
    {
        var currentCustomer = await _workContext.GetCurrentCustomerAsync();
        //try to get store identifier from attributes
        var storeScope = await _genericAttributeService
            .GetAttributeAsync<int>(currentCustomer, "SSOStoreScopeConfigurationAttribute");
        return storeScope;
    }

    /// <inheritdoc />
    public async Task ApplyModelToSettingsAsync(ConfigurationModel model)
    {
        int storeScope = await GetActiveStoreScopeSSOAsync();
        var systemWide = await _settingService.LoadSettingAsync<SsoSystemSettings>(storeId: 0);

        if (storeScope > 0)
            await SavePerStoreAsync(model, storeScope);

        systemWide.SsoBypassToken = ReprotectOrClear(model.SsoBypassToken, systemWide.SsoBypassToken, model.ClearSsoBypassToken);
        systemWide.LocalLoginAllowedRoles = model.LocalLoginAllowedRoles?.Trim() ?? string.Empty;
        systemWide.LoginAttemptCapPerHour = Math.Max(0, model.LoginAttemptCapPerHour);
        systemWide.EnableAuditLogging = model.EnableAuditLogging;
        await _settingService.SaveSettingAsync(systemWide, storeId: 0);

        await _settingService.ClearCacheAsync();
    }

    /// <inheritdoc />
    public async Task<int> DeleteSettingsForStoreAsync(int storeId)
    {
        if (storeId <= 0)
            return 0;

        const string keyPrefix = "ssoconfigurationsettings.";

        // 1. Delete every per-store row.
        var all = await _settingService.GetAllSettingsAsync();
        var toDelete = all
            .Where(s => s.StoreId == storeId
                && s.Name.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var row in toDelete)
            await _settingService.DeleteSettingAsync(row);

        // 2. Write default values back at this store with overrideForStore = true.
        //    Without these explicit per-store rows, nop's LoadSettingAsync<T>(storeId)
        //    falls back to the storeId=0 row (loadSharedValueIfNotFound: true is the
        //    default), so the admin would see whatever's seeded at install time or any
        //    legacy "All stores" values rather than a clean slate. Writing the defaults
        //    back as explicit per-store rows shadows that fallback.
        var defaults = new SsoConfigurationSettings();
        await SavePerStoreDefaultsAsync(defaults, storeId);

        await _settingService.ClearCacheAsync();
        return toDelete.Count;
    }

    /// <inheritdoc />
    public async Task SeedSettingsForStoreAsync(SsoConfigurationSettings settings, int storeId)
    {
        if (settings is null || storeId <= 0)
            return;

        await SaveAllPropertiesPerStoreAsync(settings, storeId);
        await _settingService.ClearCacheAsync();
    }

    #endregion

    #region Utilities - save paths

    private async Task SavePerStoreAsync(ConfigurationModel model, int storeScope)
    {
        var perStore = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(storeScope);

        perStore.Protocol = model.Protocol;
        perStore.IsEnabled = model.IsEnabled;
        perStore.BaseUrl = model.BaseUrl?.Trim() ?? string.Empty;
        perStore.DisplayOrder = model.DisplayOrder;
        perStore.IdpDisplayName = model.IdpDisplayName?.Trim() ?? string.Empty;
        perStore.EnableMockMode = model.EnableMockMode;

        perStore.Authority = model.Authority?.Trim() ?? string.Empty;
        perStore.ClientId = model.ClientId?.Trim() ?? string.Empty;
        perStore.ClientSecret = ReprotectOrClear(model.ClientSecret, perStore.ClientSecret, model.ClearClientSecret);
        perStore.CallbackPath = model.CallbackPath?.Trim() ?? string.Empty;
        perStore.Scopes = model.Scopes?.Trim() ?? string.Empty;

        perStore.IdpEntityId = model.IdpEntityId?.Trim() ?? string.Empty;
        perStore.IdpSsoUrl = model.IdpSsoUrl?.Trim() ?? string.Empty;
        perStore.IdpSloUrl = model.IdpSloUrl?.Trim() ?? string.Empty;
        perStore.IdpCertificateBase64 = ReprotectOrClear(model.IdpCertificateBase64, perStore.IdpCertificateBase64, model.ClearIdpCertificate);
        perStore.IdpMetadataUrl = model.IdpMetadataUrl?.Trim() ?? string.Empty;
        perStore.SpEntityId = model.SpEntityId?.Trim() ?? string.Empty;
        perStore.AcsUrl = model.AcsUrl?.Trim() ?? string.Empty;
        perStore.SpLogoutUrl = model.SpLogoutUrl?.Trim() ?? string.Empty;
        perStore.SpPfxBase64 = ReprotectOrClear(model.SpPfxBase64, perStore.SpPfxBase64, model.ClearSpPfx);
        perStore.SpPfxPassword = ReprotectOrClear(model.SpPfxPassword, perStore.SpPfxPassword, model.ClearSpPfxPassword);
        perStore.SignedRequest = model.SignedRequest;
        perStore.SignLogoutRequest = model.SignLogoutRequest;
        perStore.EncryptedAssertion = model.EncryptedAssertion;
        perStore.LoginBinding = model.LoginBinding?.Trim() ?? string.Empty;
        perStore.LogoutBinding = model.LogoutBinding?.Trim() ?? string.Empty;

        perStore.EmailClaimType = model.EmailClaimType?.Trim() ?? string.Empty;
        perStore.UsernameClaimType = model.UsernameClaimType?.Trim() ?? string.Empty;
        perStore.FirstNameClaimType = model.FirstNameClaimType?.Trim() ?? string.Empty;
        perStore.LastNameClaimType = model.LastNameClaimType?.Trim() ?? string.Empty;
        perStore.GroupsClaimType = model.GroupsClaimType?.Trim() ?? string.Empty;

        perStore.AutoRegister = model.AutoRegister;
        
        // Convert selected role IDs to comma-separated string
        perStore.DefaultRoleSystemName = model.SelectedCustomerRoleIds != null && model.SelectedCustomerRoleIds.Any()
            ? string.Join(",", model.SelectedCustomerRoleIds)
            : string.Empty;
        
        perStore.AllowedEmailDomains = model.AllowedEmailDomains?.Trim() ?? string.Empty;
        perStore.RefreshRolesOnLogin = model.RefreshRolesOnLogin;
        perStore.RedirectLoginToSso = model.RedirectLoginToSso;

        perStore.PostLoginRedirectUrl = model.PostLoginRedirectUrl?.Trim() ?? string.Empty;
        perStore.PostLogoutRedirectUrl = model.PostLogoutRedirectUrl?.Trim() ?? string.Empty;

        perStore.CustomAttributesJson = model.CustomAttributesJson?.Trim() ?? string.Empty;

        await SaveAllPropertiesPerStoreAsync(perStore, storeScope);
    }

    private async Task SavePerStoreDefaultsAsync(SsoConfigurationSettings defaults, int storeScope)
    {
        await SaveAllPropertiesPerStoreAsync(defaults, storeScope);
    }

    private async Task SaveAllPropertiesPerStoreAsync(SsoConfigurationSettings src, int storeScope)
    {
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.Protocol, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.IsEnabled, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.BaseUrl, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.DisplayOrder, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.IdpDisplayName, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.EnableMockMode, true, storeScope, false);

        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.Authority, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.ClientId, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.ClientSecret, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.CallbackPath, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.Scopes, true, storeScope, false);

        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.IdpEntityId, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.IdpSsoUrl, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.IdpSloUrl, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.IdpCertificateBase64, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.IdpMetadataUrl, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.SpEntityId, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.AcsUrl, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.SpLogoutUrl, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.SpPfxBase64, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.SpPfxPassword, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.SignedRequest, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.SignLogoutRequest, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.EncryptedAssertion, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.LoginBinding, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.LogoutBinding, true, storeScope, false);

        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.EmailClaimType, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.UsernameClaimType, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.FirstNameClaimType, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.LastNameClaimType, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.GroupsClaimType, true, storeScope, false);

        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.AutoRegister, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.DefaultRoleSystemName, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.AllowedEmailDomains, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.RefreshRolesOnLogin, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.RedirectLoginToSso, true, storeScope, false);

        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.PostLoginRedirectUrl, true, storeScope, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.PostLogoutRedirectUrl, true, storeScope, false);

        await _settingService.SaveSettingOverridablePerStoreAsync(src, x => x.CustomAttributesJson, true, storeScope, false);
    }

    #endregion

    #region Utilities

    private string ReprotectIfChanged(string posted, string existing)
    {
        if (string.IsNullOrWhiteSpace(posted))
            return existing ?? string.Empty;

        return _secretsService.Protect(posted.Trim());
    }

    private string ReprotectOrClear(string posted, string existing, bool clear)
    {
        if (clear) return string.Empty;
        return ReprotectIfChanged(posted, existing);
    }

    private string BuildSecretPreview(string? ciphertext, int contextChars = 10)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
            return string.Empty;

        if (!_secretsService.TryUnprotect(ciphertext, out var plaintext) || string.IsNullOrWhiteSpace(plaintext))
            return "(stored)";

        var stripped = new string(plaintext.Where(c => !char.IsWhiteSpace(c)).ToArray());

        if (stripped.Length <= contextChars * 2)
            return stripped;

        return $"{stripped[..contextChars]}...{stripped[^contextChars..]}";
    }

    #endregion
}
