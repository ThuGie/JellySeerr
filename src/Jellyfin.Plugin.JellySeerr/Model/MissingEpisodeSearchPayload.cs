namespace Jellyfin.Plugin.JellySeerr.Model;

public sealed class MissingEpisodeSearchPayload
{
    public int SeasonNumber { get; set; }
    public List<int>? EpisodeIds { get; set; }
}
