using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Services;

namespace Nop.Plugin.ExternalAuth.SSO.Infrastructure.Oidc;

/// <summary>
/// Options cache for <see cref="OpenIdConnectOptions"/> that keeps one entry per store for the
/// plugin's scheme, so every store signs in against its own IdP registration.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core's <c>OptionsMonitor</c> asks this cache for the scheme's options on every request
/// (the OIDC handler is initialised per request). The default cache keys by scheme name only, so
/// the first store to build the options would serve every store until the process restarts.
/// Keying by <c>scheme|storeId</c> lets the factory build a separate options object per store:
/// <see cref="SsoOidcOptionsConfigurer"/> fills it from that store's settings, then the framework's
/// own post-configure step gives it its own discovery-document manager.
/// </para>
/// <para>
/// Entries are dropped by <see cref="InvalidateStore"/> when settings change, and expire after
/// <see cref="EntryLifetime"/> as a backstop for web farms, where a save on one server does not
/// reach the others. Names other than the plugin's scheme are cached by name, as the default
/// cache does.
/// </para>
/// </remarks>
public class StoreAwareOidcOptionsCache : IOptionsMonitorCache<OpenIdConnectOptions>, ISsoOidcOptionsCache
{
    #region Fields

    /// <summary>How long a store's options live before they are rebuilt from settings.</summary>
    public static readonly TimeSpan EntryLifetime = TimeSpan.FromMinutes(15);

    private const char KeySeparator = '|';

    protected readonly ConcurrentDictionary<string, CacheEntry> _entries = new(StringComparer.Ordinal);
    protected readonly IHttpContextAccessor _httpContextAccessor;

    #endregion

    #region Ctor

    public StoreAwareOidcOptionsCache(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    #endregion

    #region Utilities

    protected sealed record CacheEntry(Lazy<OpenIdConnectOptions> Options, DateTime CreatedOnUtc)
    {
        public bool IsExpired => DateTime.UtcNow - CreatedOnUtc > EntryLifetime;
    }

    private static CacheEntry NewEntry(Func<OpenIdConnectOptions> createOptions)
        => new(new Lazy<OpenIdConnectOptions>(createOptions, LazyThreadSafetyMode.ExecutionAndPublication), DateTime.UtcNow);

    private string BuildKey(string? name)
    {
        name ??= Microsoft.Extensions.Options.Options.DefaultName;

        if (!string.Equals(name, SSOAuthenticationDefaults.OidcAuthenticationScheme, StringComparison.Ordinal))
            return name;

        return $"{name}{KeySeparator}{GetCurrentStoreId()}";
    }

    /// <summary>
    /// The current request's store. Blocks on the async store context, as nop's own settings
    /// registration does — options configuration is synchronous and the store context caches the
    /// store for the request, so this does not reach the database twice.
    /// </summary>
    private int GetCurrentStoreId()
    {
        var storeContext = _httpContextAccessor.HttpContext?.RequestServices.GetService<IStoreContext>();

        return storeContext?.GetCurrentStoreAsync().GetAwaiter().GetResult()?.Id ?? 0;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public OpenIdConnectOptions GetOrAdd(string? name, Func<OpenIdConnectOptions> createOptions)
    {
        var key = BuildKey(name);
        var entry = _entries.GetOrAdd(key, _ => NewEntry(createOptions));

        if (entry.IsExpired)
        {
            var fresh = NewEntry(createOptions);
            entry = _entries.TryUpdate(key, fresh, entry) ? fresh : _entries.GetOrAdd(key, _ => fresh);
        }

        return entry.Options.Value;
    }

    /// <inheritdoc />
    public bool TryAdd(string? name, OpenIdConnectOptions options)
        => _entries.TryAdd(BuildKey(name), new CacheEntry(new Lazy<OpenIdConnectOptions>(options), DateTime.UtcNow));

    /// <inheritdoc />
    public bool TryRemove(string? name)
    {
        name ??= Microsoft.Extensions.Options.Options.DefaultName;

        var removed = false;
        foreach (var key in _entries.Keys.Where(k => k == name || k.StartsWith(name + KeySeparator, StringComparison.Ordinal)))
            removed |= _entries.TryRemove(key, out _);

        return removed;
    }

    /// <inheritdoc />
    public void Clear() => _entries.Clear();

    /// <inheritdoc />
    public void InvalidateStore(int storeId)
    {
        if (storeId == 0)
        {
            TryRemove(SSOAuthenticationDefaults.OidcAuthenticationScheme);
            return;
        }

        _entries.TryRemove($"{SSOAuthenticationDefaults.OidcAuthenticationScheme}{KeySeparator}{storeId}", out _);
    }

    #endregion
}
