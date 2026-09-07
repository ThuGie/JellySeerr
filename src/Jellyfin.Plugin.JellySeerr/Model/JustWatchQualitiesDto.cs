namespace Jellyfin.Plugin.JellySeerr.Model;

public sealed class JustWatchQualitiesDto
{
    public string HighestReleasedQuality { get; init; } = string.Empty;

    public string MostCommonQuality { get; init; } = string.Empty;
}
