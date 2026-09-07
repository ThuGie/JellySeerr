namespace Jellyfin.Plugin.JellySeerr.Configuration;

public class DisplayCustomizations
{
    public DisplayItemStyle StreamingService { get; set; } = new();

    public DisplayItemStyle StudioNetwork { get; set; } = new();

    public DisplayItemStyle GenreBackdrop { get; set; } = new() { DuotoneEnabled = false };

    public DisplayItemStyle DiscoverBackdrop { get; set; } = new() { DuotoneEnabled = false };
}
