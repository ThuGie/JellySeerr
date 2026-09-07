using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.JellySeerr.Configuration;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Configuration.Advanced;

public static class AdvancedSettingsHelper
{
    private static readonly Regex SafeImageSizePattern = new(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled);

    public static AdvancedSettings Resolve(PluginConfiguration config)
    {
        AdvancedSettings advanced = config.Advanced ?? new AdvancedSettings();
        config.Advanced = advanced;
        advanced.Discovery ??= new AdvancedDiscoverySettings();
        advanced.Carousel ??= new AdvancedCarouselSettings();
        advanced.Requests ??= new AdvancedRequestsSettings();
        advanced.RequestModal ??= new AdvancedRequestModalSettings();
        advanced.Servarr ??= new AdvancedServarrSettings();
        advanced.Tmdb ??= new AdvancedTmdbSettings();
        advanced.JustWatch ??= new AdvancedJustWatchSettings();
        advanced.Letterboxd ??= new AdvancedLetterboxdSettings();
        Clamp(advanced);
        return advanced;
    }

    public static void Clamp(AdvancedSettings advanced)
    {
        AdvancedDiscoverySettings d = advanced.Discovery;
        d.CarouselMaxJellyseerrPages = Clamp(d.CarouselMaxJellyseerrPages, 1, 50);
        d.GridMaxJellyseerrPages = Clamp(d.GridMaxJellyseerrPages, 1, 100);
        d.GridPageSize = Clamp(d.GridPageSize, 1, 200);
        if (string.IsNullOrWhiteSpace(d.AnimeDiscoverPath))
        {
            d.AnimeDiscoverPath = "/api/v1/discover/tv?genre=16&keywords=210024";
        }

        AdvancedCarouselSettings c = advanced.Carousel;
        c.CarouselScrollThreshold = Clamp(c.CarouselScrollThreshold, 100, 10000);
        c.RowScrollBindRetries = Clamp(c.RowScrollBindRetries, 1, 50);

        AdvancedRequestsSettings r = advanced.Requests;
        r.PageSize = Clamp(r.PageSize, 1, 100);
        r.FetchSize = Clamp(r.FetchSize, r.PageSize, 500);
        r.AutoRefreshIntervalSeconds = Clamp(r.AutoRefreshIntervalSeconds, 0, 3600);

        AdvancedTmdbSettings t = advanced.Tmdb;
        t.BackdropBatchConcurrency = Clamp(t.BackdropBatchConcurrency, 1, 20);
        t.BackdropImageSize = SanitizeImageSize(t.BackdropImageSize, "w780");
        t.PosterImageSize = SanitizeImageSize(t.PosterImageSize, "w600_and_h900_bestv2");
        if (string.IsNullOrWhiteSpace(t.BackdropLanguageFilter))
        {
            t.BackdropLanguageFilter = "en,null,en-US";
        }

        AdvancedJustWatchSettings jw = advanced.JustWatch;
        jw.SearchResultLimit = Clamp(jw.SearchResultLimit, 1, 50);
        if (string.IsNullOrWhiteSpace(jw.Country))
        {
            jw.Country = "US";
        }

        if (string.IsNullOrWhiteSpace(jw.Language))
        {
            jw.Language = "en";
        }
    }

    public static string ResolveJustWatchCountry(PluginConfiguration config)
    {
        AdvancedJustWatchSettings jw = Resolve(config).JustWatch;
        if (jw.UseWatchRegionForCountry && !string.IsNullOrWhiteSpace(config.WatchRegion))
        {
            return config.WatchRegion.Trim();
        }

        return jw.Country;
    }

    public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

    private static string SanitizeImageSize(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || !SafeImageSizePattern.IsMatch(value) ? fallback : value.Trim();

    private static readonly Dictionary<string, string[]> DefaultQualityLabelAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ultra-HD"] = new[] { "Ultra-HD", "Ultra HD", "4K", "UHD", "2160p" },
        ["HD - 720p/1080p"] = new[] { "HD - 720p/1080p", "HD", "720p", "1080p", "HD-720p/1080p" },
        ["SD"] = new[] { "SD", "DVD", "480p" }
    };

    public static object BuildFrontendPayload(PluginConfiguration config)
    {
        AdvancedSettings advanced = Resolve(config);
        return new
        {
            discovery = new { gridPageSize = advanced.Discovery.GridPageSize },
            carousel = new
            {
                carouselScrollThreshold = advanced.Carousel.CarouselScrollThreshold,
                discoverRowFocusScale = advanced.Carousel.DiscoverRowFocusScale,
                browseCarouselFocusScale = advanced.Carousel.BrowseCarouselFocusScale,
                enableCenterFocus = advanced.Carousel.EnableCenterFocus,
                enableRowInfiniteScroll = advanced.Carousel.EnableRowInfiniteScroll,
                rowScrollBindRetries = advanced.Carousel.RowScrollBindRetries
            },
            requests = new
            {
                pageSize = advanced.Requests.PageSize,
                fetchSize = advanced.Requests.FetchSize,
                cardsInteractive = advanced.Requests.CardsInteractive,
                cardsIncludeMetaText = advanced.Requests.CardsIncludeMetaText,
                includePartialsInProcessingFilter = advanced.Requests.IncludePartialsInProcessingFilter,
                splitPartiallyAvailableFilter = advanced.Requests.SplitPartiallyAvailableFilter,
                autoRefreshIntervalSeconds = advanced.Requests.AutoRefreshIntervalSeconds,
                refreshOnVisibility = advanced.Requests.RefreshOnVisibility,
                refreshOnTabShow = advanced.Requests.RefreshOnTabShow
            },
            requestModal = new
            {
                tvSeasonPickerEnabled = advanced.RequestModal.TvSeasonPickerEnabled,
                includeSpecialsSeason = advanced.RequestModal.IncludeSpecialsSeason,
                requireExplicitSeasonSelection = advanced.RequestModal.RequireExplicitSeasonSelection,
                showRequest4kButton = advanced.RequestModal.ShowRequest4kButton && !config.Disable4k,
                backdropLanguageFilter = advanced.Tmdb.BackdropLanguageFilter
            },
            tmdb = new
            {
                backdropImageSize = advanced.Tmdb.BackdropImageSize,
                posterImageSize = advanced.Tmdb.PosterImageSize,
                backdropLanguageFilter = advanced.Tmdb.BackdropLanguageFilter,
                preferOriginalLanguageImages = advanced.Tmdb.PreferOriginalLanguageImages,
                genreBackdropSelectionMode = advanced.Tmdb.GenreBackdropSelectionMode,
                fallbackToOriginalImageUrl = advanced.Tmdb.FallbackToOriginalImageUrl,
                directBrowserImages = advanced.Tmdb.DirectBrowserImages
            }
        };
    }

    public static Dictionary<string, string[]> GetQualityLabelAliases(PluginConfiguration config)
    {
        Dictionary<string, string[]> merged = new(DefaultQualityLabelAliases, StringComparer.OrdinalIgnoreCase);
        string? json = Resolve(config).JustWatch.QualityAliasJson;
        if (string.IsNullOrWhiteSpace(json))
        {
            return merged;
        }

        try
        {
            JObject? custom = JObject.Parse(json);
            if (custom == null)
            {
                return merged;
            }

            foreach (JProperty property in custom.Properties())
            {
                if (property.Value is JArray array)
                {
                    merged[property.Name] = array.Select(v => v.ToString()).Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
                }
            }
        }
        catch (JsonException)
        {
        }

        return merged;
    }
}
