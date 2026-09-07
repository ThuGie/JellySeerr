namespace Jellyfin.Plugin.JellySeerr.Configuration.Advanced;

public class AdvancedSettings
{
    public AdvancedDiscoverySettings Discovery { get; set; } = new();

    public AdvancedCarouselSettings Carousel { get; set; } = new();

    public AdvancedRequestsSettings Requests { get; set; } = new();

    public AdvancedRequestModalSettings RequestModal { get; set; } = new();

    public AdvancedServarrSettings Servarr { get; set; } = new();

    public AdvancedTmdbSettings Tmdb { get; set; } = new();

    public AdvancedJustWatchSettings JustWatch { get; set; } = new();

    public AdvancedLetterboxdSettings Letterboxd { get; set; } = new();
}

public class AdvancedLetterboxdSettings
{
    public string AlreadyRequestedStatusScope { get; set; } = "anyMediaInfo";
}
