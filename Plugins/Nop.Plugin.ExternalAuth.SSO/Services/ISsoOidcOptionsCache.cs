namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Drops the cached OpenID Connect options of a store so the next request rebuilds them from
/// the store's current settings.
/// </summary>
/// <remarks>
/// The options themselves are cached per store by
/// <see cref="Infrastructure.Oidc.StoreAwareOidcOptionsCache"/>. Call this after any write to a
/// store's <see cref="Settings.SsoConfigurationSettings"/> (save, delete, seed).
/// </remarks>
public interface ISsoOidcOptionsCache
{
    /// <summary>
    /// Forget the OIDC options built for one store.
    /// </summary>
    /// <param name="storeId">Store identifier; 0 forgets every store, because store 0 values are inherited by any store without its own</param>
    void InvalidateStore(int storeId);
}
