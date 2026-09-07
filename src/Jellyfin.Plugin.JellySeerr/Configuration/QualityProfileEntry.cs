namespace Jellyfin.Plugin.JellySeerr.Configuration;

public class QualityProfileEntry
{
    public string ServerType { get; set; } = "radarr";

    public int ServerId { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public bool Is4k { get; set; }

    public bool IsAnime { get; set; }

    public int ProfileId { get; set; }

    public string ProfileName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public int SortOrder { get; set; }

    public bool IsDefaultMovie { get; set; }

    public bool IsDefaultTv { get; set; }

    public bool IsDefaultMovie4k { get; set; }

    public bool IsDefaultTv4k { get; set; }

    public bool IsDefaultAnime { get; set; }

    public string? DefaultRootFolder { get; set; }

    public string Key => $"{ServerType}:{ServerId}:{ProfileId}";
}
