namespace Jellyfin.Plugin.JellySeerr.Configuration.Advanced;

public class AdvancedTmdbSettings
{
    public int BackdropBatchConcurrency { get; set; } = 5;

    public string BackdropImageSize { get; set; } = "w780";

    public string PosterImageSize { get; set; } = "w600_and_h900_bestv2";

    public string BackdropLanguageFilter { get; set; } = "en,null,en-US";

    public bool PreferOriginalLanguageImages { get; set; }

    public string GenreBackdropSelectionMode { get; set; } = "random";

    public bool FallbackToOriginalImageUrl { get; set; } = true;

    public bool DirectBrowserImages { get; set; }
}