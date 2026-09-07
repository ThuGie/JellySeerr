namespace Jellyfin.Plugin.JellySeerr.Configuration;

public class DisplayItemStyle
{
    public bool DuotoneEnabled { get; set; } = true;

    public string DuotoneLight { get; set; } = "ffffff"; // first color

    public string DuotoneDark { get; set; } = "969696"; // second color
}