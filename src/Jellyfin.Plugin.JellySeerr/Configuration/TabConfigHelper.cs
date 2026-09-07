namespace Jellyfin.Plugin.JellySeerr.Configuration;

public static class TabConfigHelper
{
    private const string JellyfinHomeKey = "jf:home";
    private const string JellyfinFavoritesKey = "jf:favorites";

    private static readonly HashSet<string> KnownTabIds = new(
        TabConfig.CreateDefaults().Select(tab => tab.Id),
        StringComparer.OrdinalIgnoreCase);

    public static List<TabConfig> Normalize(IEnumerable<TabConfig>? tabs)
    {
        var defaults = TabConfig.CreateDefaults();
        if (tabs == null)
        {
            return defaults;
        }

        var enabledById = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var titleById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (TabConfig tab in tabs)
        {
            string id = (tab.Id ?? string.Empty).Trim().ToLowerInvariant();
            if (!KnownTabIds.Contains(id))
            {
                continue;
            }

            enabledById[id] = tab.Enabled;
            string title = (tab.Title ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(title))
            {
                titleById[id] = title;
            }
        }

        foreach (TabConfig tab in defaults)
        {
            if (enabledById.TryGetValue(tab.Id, out bool enabled))
            {
                tab.Enabled = enabled;
            }

            if (titleById.TryGetValue(tab.Id, out string? title))
            {
                tab.Title = title;
            }
        }

        return defaults;
    }

    private static string PluginKey(string id) => $"js:{id.Trim().ToLowerInvariant()}";

    private static string CustomTabsKey(int index) => $"ct:{index}";

    public static List<string> NormalizeBarOrder(IEnumerable<string>? barOrder)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void TryAdd(string key)
        {
            if (seen.Add(key))
            {
                result.Add(key);
            }
        }

        TryAdd(JellyfinHomeKey);
        TryAdd(JellyfinFavoritesKey);

        foreach (string raw in barOrder ?? Array.Empty<string>())
        {
            string key = (raw ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (KnownTabIds.Contains(key))
            {
                key = PluginKey(key);
            }

            if (key.StartsWith("sf:", StringComparison.OrdinalIgnoreCase) &&
                KnownTabIds.Contains(key[3..]))
            {
                key = PluginKey(key[3..]);
            }

            if (string.Equals(key, JellyfinHomeKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, JellyfinFavoritesKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (key.StartsWith("js:", StringComparison.OrdinalIgnoreCase) &&
                KnownTabIds.Contains(key[3..]))
            {
                TryAdd(PluginKey(key[3..]));
                continue;
            }

            if (key.StartsWith("ct:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(key[3..], out int index) &&
                index >= 0)
            {
                TryAdd(CustomTabsKey(index));
            }
        }

        foreach (TabConfig tab in TabConfig.CreateDefaults())
        {
            TryAdd(PluginKey(tab.Id));
        }

        return result;
    }
}
