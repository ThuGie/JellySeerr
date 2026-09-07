using Jellyfin.Plugin.JellySeerr.Configuration;
using Jellyfin.Plugin.JellySeerr.Configuration.Advanced;
using Jellyfin.Plugin.JellySeerr.Services;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellySeerr.Helpers;

public static class ImageCacheHelper
{
    public static string GetCachedImageUrl(
        ImageCacheService imageCacheService,
        string? sourceUrl,
        ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(sourceUrl))
        {
            return string.Empty;
        }

        PluginConfiguration? config = JellySeerrPlugin.Instance?.Configuration;
        try
        {
            if (config != null && AdvancedSettingsHelper.Resolve(config).Tmdb.DirectBrowserImages)
            {
                return sourceUrl;
            }

            int cacheTimeout = config?.CacheTimeoutSeconds ?? 86400;

            // Used in discovery mapping which allows cached images to be used in discovery cards
            string? cacheKey = imageCacheService.GetOrCacheImage(sourceUrl, cacheTimeout)
                .GetAwaiter()
                .GetResult();

            if (!string.IsNullOrEmpty(cacheKey))
            {
                return $"/JellySeerr/CachedImage/{cacheKey}";
            }

            bool fallback = config == null || AdvancedSettingsHelper.Resolve(config).Tmdb.FallbackToOriginalImageUrl;
            if (fallback)
            {
                logger?.LogWarning("JS • failed to cache image from {SourceUrl}, using original URL", sourceUrl);
                return sourceUrl;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "JS • error caching image from {SourceUrl}", sourceUrl);
            bool fallback = config == null || AdvancedSettingsHelper.Resolve(config).Tmdb.FallbackToOriginalImageUrl;
            return fallback ? sourceUrl : string.Empty;
        }
    }
}
