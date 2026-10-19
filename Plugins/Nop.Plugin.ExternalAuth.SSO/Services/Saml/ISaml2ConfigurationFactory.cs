using ITfoxtec.Identity.Saml2;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Builds a fresh <see cref="Saml2Configuration"/> per request from the per-store
/// <see cref="Settings.SsoConfigurationSettings"/>.
/// </summary>
/// <remarks>
/// ITfoxtec's <c>Saml2Configuration</c> is plain CLR state — we build a new instance every
/// time so per-store settings flow through naturally without the options-cache wrinkle
/// the OIDC handler has (the trade-off that drove the ITfoxtec-over-Sustainsys decision
/// in SSOPlan.v2.md §2 row 2).
/// </remarks>
public interface ISaml2ConfigurationFactory
{
    /// <summary>
    /// Build the configuration for the current store. Decrypts IdP cert + SP PFX inline.
    /// </summary>
    Task<Saml2Configuration> CreateForCurrentStoreAsync();
}
