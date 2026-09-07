namespace Jellyfin.Plugin.JellySeerr.Configuration.Advanced;

public class AdvancedCarouselSettings
{
    public int CarouselScrollThreshold { get; set; } = 1200;

    public bool DiscoverRowFocusScale { get; set; } = true;

    public bool BrowseCarouselFocusScale { get; set; }

    public bool EnableCenterFocus { get; set; } = true;

    public bool EnableRowInfiniteScroll { get; set; } = true;

    public int RowScrollBindRetries { get; set; } = 10;
}