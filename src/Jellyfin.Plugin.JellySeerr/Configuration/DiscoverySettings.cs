namespace Jellyfin.Plugin.JellySeerr.Configuration.Advanced;

public class AdvancedDiscoverySettings
{
    public bool ApplyLanguageFilter { get; set; } = true;

    public bool HideRequestedMedia { get; set; }

    public bool HideAvailableInLibrary { get; set; }

    public bool HideAdultContent { get; set; } = true;

    public bool UseSeerrMappingForAnime { get; set; } = true;

    public string AnimeDiscoverPath { get; set; } = "/api/v1/discover/tv?genre=16&keywords=210024";

    public int CarouselMaxJellyseerrPages { get; set; } = 5;

    public int GridMaxJellyseerrPages { get; set; } = 20;

    public int GridPageSize { get; set; } = 40;
}