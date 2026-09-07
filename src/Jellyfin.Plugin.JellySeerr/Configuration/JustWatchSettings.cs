namespace Jellyfin.Plugin.JellySeerr.Configuration.Advanced;

public class AdvancedJustWatchSettings
{
    public bool UseWatchRegionForCountry { get; set; }

    public string Country { get; set; } = "US";

    public string Language { get; set; } = "en";

    public int SearchResultLimit { get; set; } = 10;

    public bool FallbackToDefaultProfile { get; set; } = true;

    public bool Prefer4kServerForUltraHd { get; set; } = true;

    public string QualityAliasJson { get; set; } = string.Empty;
}