using Nop.Core;
using Nop.Core.Caching;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Services.Helpers;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="ISsoRateLimiter"/>.
/// </summary>
public class SsoRateLimiter : ISsoRateLimiter
{
    /// <summary>Sliding window for the counter — 1 hour to match the per-hour cap semantics.</summary>
    private static readonly TimeSpan WindowDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// Cache key prefix. <c>IStaticCacheManager</c> supports per-prefix invalidation; the
    /// prefix lets us flush all rate-limit state if needed without touching unrelated cache
    /// entries.
    /// </summary>
    private const string CacheKeyPrefix = "Nop.Plugin.ExternalAuth.SSO.RateLimit.";

    #region Fields

    protected readonly IStaticCacheManager _cacheManager;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public SsoRateLimiter(
        IStaticCacheManager cacheManager,
        ISettingService settingService,
        IWebHelper webHelper)
    {
        _cacheManager = cacheManager;
        _settingService = settingService;
        _webHelper = webHelper;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<bool> CheckAndIncrementAsync(string bucket = ISsoRateLimiter.LoginBucket)
    {
        var settings = await _settingService.LoadSettingAsync<SsoSystemSettings>(storeId: 0);
        if (settings.LoginAttemptCapPerHour <= 0)
            return true;

        var ip = _webHelper.GetCurrentIpAddress();
        if (string.IsNullOrWhiteSpace(ip))
            return true; // cannot rate-limit without an IP; fail open rather than break login

        var cacheKey = new CacheKey($"{CacheKeyPrefix}{bucket}.{ip}")
        {
            CacheTime = (int)WindowDuration.TotalMinutes
        };

        var current = await _cacheManager.GetAsync<int>(cacheKey, () => 0);
        var next = current + 1;

        if (next > settings.LoginAttemptCapPerHour)
            return false;

        await _cacheManager.SetAsync(cacheKey, next);
        return true;
    }

    #endregion
}
