using Jellyfin.Plugin.JellySeerr.Configuration.Advanced;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellySeerr.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string? JellyseerrUrl { get; set; } = string.Empty;

    public string? ExternalJellyseerrUrl { get; set; } = string.Empty;

    public string? JellyseerrApiKey { get; set; } = string.Empty;

    public string? RadarrUrl { get; set; } = string.Empty;

    public string? RadarrApiKey { get; set; } = string.Empty;

    public string? SonarrUrl { get; set; } = string.Empty;

    public string? SonarrApiKey { get; set; } = string.Empty;

    public string? JellyseerrPreferredLanguages { get; set; } = "en";

    public string? TmdbApiKey { get; set; } = string.Empty;

    public string WatchRegion { get; set; } = "US";

    public int RowItemLimit { get; set; } = 20;

    public int CacheTimeoutSeconds { get; set; } = 86400;

    public int MaxImageCacheEntries { get; set; } = 5000;

    public int DiscoveryCacheSeconds { get; set; } = 180;

    public int RequestsCacheSeconds { get; set; } = 20;

    public int ServarrCacheSeconds { get; set; } = 120;

    public bool DeveloperMode { get; set; }

    public int CacheBustCounter { get; set; }

    public bool StreamingServiceUseImages { get; set; } = true;

    public bool StudioNetworkUseImages { get; set; } = true;

    public bool GenreUseBackdrops { get; set; } = true;

    public bool DiscoverUsePosters { get; set; } = true;

    public List<int> DiscoverReleaseTypes { get; set; } = new();

    public bool ElegantFinFixes { get; set; }

    public bool QualityRecommendations { get; set; } = true;

    public bool AddSeerrResultsInSearch { get; set; } = true;

    public bool NativeCarousels { get; set; }

    public bool NativeGridPages { get; set; }

    public bool NativeSearchResults { get; set; }

    public string DisplayCustomizationsJson { get; set; } = string.Empty;

    public List<TabConfig> Tabs { get; set; } = new();

    public List<string> TabBarOrder { get; set; } = new();

    public AdvancedSettings? Advanced { get; set; }

    public List<QualityProfileEntry> QualityProfiles { get; set; } = new();

    public bool ExposeProfilesToEveryone { get; set; } = true;

    public bool Disable4k { get; set; }

    public bool HideUnmappedUsers { get; set; } = true;

    public bool ConfirmCancel { get; set; } = true;

    public bool ShowQuotaWarnings { get; set; } = true;

    public bool EnableManagerTools { get; set; } = true;
}
