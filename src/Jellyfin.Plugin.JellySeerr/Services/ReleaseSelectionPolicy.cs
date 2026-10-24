using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

/// <summary>Interactive grabs bypass Arr's decision engine, so enforce it before sending a grab.</summary>
public static class ReleaseSelectionPolicy
{
    public static bool CanAccess(JObject request, int userId, bool manager) =>
        request.Value<int?>("status") is 2 or 5
        && (manager || request["requestedBy"]?.Value<int?>("id") == userId);

    public static bool IsDiskRejection(string reason) =>
        reason.StartsWith("Existing file meets cutoff:", StringComparison.OrdinalIgnoreCase)
        || reason.StartsWith("Existing file on disk is of equal or higher preference:", StringComparison.OrdinalIgnoreCase)
        || reason.StartsWith("Existing file on disk is of equal or higher revision:", StringComparison.OrdinalIgnoreCase)
        || reason.StartsWith("Existing file on disk meets quality cutoff:", StringComparison.OrdinalIgnoreCase)
        || reason.StartsWith("Existing file on disk meets Custom Format cutoff:", StringComparison.OrdinalIgnoreCase)
        || reason.StartsWith("Existing file on disk has a equal or higher Custom Format score:", StringComparison.OrdinalIgnoreCase)
        || reason.StartsWith("Existing file on disk has Custom Format score within Custom Format score increment:", StringComparison.OrdinalIgnoreCase)
        || reason.StartsWith("Existing file on disk and Quality Profile '", StringComparison.OrdinalIgnoreCase)
            && reason.EndsWith("' does not allow upgrades", StringComparison.OrdinalIgnoreCase);

    public static bool CanSelect(JObject release, bool replacing, bool allow4k)
    {
        if (string.IsNullOrWhiteSpace(release.Value<string>("guid")) || release.Value<int?>("indexerId") is not > 0)
            return false;
        if (!allow4k && release["quality"]?["quality"]?.Value<int?>("resolution") is >= 2160)
            return false;
        string[] reasons = (release["rejections"] as JArray)?.Values<string>().Where(x => x != null).Select(x => x!).ToArray() ?? [];
        if (reasons.Length > 0)
            return replacing && reasons.All(IsDiskRejection);
        return release.Value<bool?>("approved") == true && release.Value<bool?>("rejected") != true;
    }

    // Episode searches can also return season packs or multi-episode releases. Do not let a
    // replacement of one episode affect other episodes or seasons owned by another requester.
    public static bool IsSingleEpisode(JObject release, JObject episode)
    {
        if (release.Value<bool?>("fullSeason") == true) return false;
        int? season = release.Value<int?>("mappedSeasonNumber") ?? release.Value<int?>("seasonNumber");
        JArray? numbers = release["mappedEpisodeNumbers"] as JArray;
        if (numbers == null || numbers.Count == 0) numbers = release["episodeNumbers"] as JArray;
        return season == episode.Value<int?>("seasonNumber") && numbers?.Count == 1
            && numbers[0].Value<int>() == episode.Value<int?>("episodeNumber");
    }

    public static JObject? FindGrab(JArray history, JObject file)
    {
        string? path = file.Value<string>("path");
        JObject? imported = history.OfType<JObject>()
            .Where(h => string.Equals(h.Value<string>("eventType"), "downloadFolderImported", StringComparison.OrdinalIgnoreCase)
                || h.Value<string>("eventType") == "3")
            .Where(h => !string.IsNullOrEmpty(path) && string.Equals(h["data"]?.Value<string>("importedPath"), path, StringComparison.Ordinal))
            .OrderByDescending(h => h.Value<DateTime?>("date")).FirstOrDefault();
        string? downloadId = imported?.Value<string>("downloadId");
        string? sceneName = file.Value<string>("sceneName");
        return history.OfType<JObject>()
            .Where(h => string.Equals(h.Value<string>("eventType"), "grabbed", StringComparison.OrdinalIgnoreCase)
                || h.Value<string>("eventType") == "1")
            .Where(h => !string.IsNullOrEmpty(downloadId)
                ? h.Value<string>("downloadId") == downloadId
                : !string.IsNullOrEmpty(sceneName) && string.Equals(h.Value<string>("sourceTitle"), sceneName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(h => h.Value<DateTime?>("date")).FirstOrDefault();
    }
}
