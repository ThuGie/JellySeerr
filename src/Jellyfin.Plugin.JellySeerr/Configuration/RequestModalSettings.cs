namespace Jellyfin.Plugin.JellySeerr.Configuration.Advanced;

public class AdvancedRequestModalSettings
{
    public bool TvSeasonPickerEnabled { get; set; } = true;

    public bool IncludeSpecialsSeason { get; set; }

    public bool RequireExplicitSeasonSelection { get; set; }

    public bool ShowRequest4kButton { get; set; } = true;
}