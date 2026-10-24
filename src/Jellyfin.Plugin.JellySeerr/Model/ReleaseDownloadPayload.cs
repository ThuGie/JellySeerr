namespace Jellyfin.Plugin.JellySeerr.Model;

public class ReleaseDownloadPayload
{
    public string SearchToken { get; set; } = string.Empty;
    public int ReleaseIndex { get; set; }
    public bool RejectCurrent { get; set; }
}
