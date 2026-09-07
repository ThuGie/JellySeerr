namespace Jellyfin.Plugin.JellySeerr.Configuration;

public class TabConfig
{
    public string Id { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string Title { get; set; } = string.Empty;

    public static List<TabConfig> CreateDefaults() =>
    [
        new() { Id = "movies", Title = "Movies" },
        new() { Id = "tv", Title = "TV Shows" },
        new() { Id = "requests", Title = "Requests" }
    ];
}
