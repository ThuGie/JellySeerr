using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public static class AvailabilityPolicy
{
    public static bool CanView(JObject request, int userId, bool manager) =>
        request.Value<int?>("status") is >= 1 and <= 5
        && (manager || request["requestedBy"]?.Value<int?>("id") == userId || request.Value<string>("type") == "tv");

    public static bool QueueMatchesEpisode(JObject q, JObject episode) =>
        q.Value<int?>("episodeId") == episode.Value<int>("id")
        || q.Value<int?>("seriesId") == episode.Value<int>("seriesId") && q.Value<int?>("episodeId") is not > 0
            && (q.Value<int?>("seasonNumber") == null || q.Value<int?>("seasonNumber") == episode.Value<int>("seasonNumber"));

    public static JObject Episode(JObject episode, JArray queue, DateTimeOffset now, bool canSearch)
    {
        JObject? q = queue.OfType<JObject>().FirstOrDefault(q => QueueMatchesEpisode(q, episode));
        JObject state;
        if (episode.Value<int?>("episodeFileId") is > 0)
            state = State("available", "Available", "A file is in the library. If playback fails, choose another release.");
        else if (q != null) state = Queue(q);
        else if (!TryAirDate(episode["airDateUtc"], out DateTimeOffset airDate))
            state = State("unknown", "Air date unknown", "An air date is not available yet. Check again when the episode is scheduled.");
        else if (airDate > now)
            state = State("unaired", "Not aired yet", "The episode has not aired yet. Its air date does not guarantee immediate download availability.");
        else if (episode.Value<bool?>("monitored") != true)
            state = State("unmonitored", "Monitoring off", "This aired episode is missing, but monitoring is off. Ask a manager to enable it.");
        else state = State("missing", "Missing", "This episode has aired, but no file is in the library. You can search for a suitable release.");
        state["id"] = episode["id"];
        state["seasonNumber"] = episode["seasonNumber"];
        state["episodeNumber"] = episode["episodeNumber"];
        state["title"] = episode["title"];
        state["airDateUtc"] = episode["airDateUtc"];
        state["hasFile"] = episode.Value<int?>("episodeFileId") is > 0;
        state["canSearch"] = canSearch && state.Value<string>("statusKey") == "missing";
        return state;
    }

    public static JObject Queue(JObject item)
    {
        string state = (item.Value<string>("trackedDownloadState") ?? "").ToLowerInvariant();
        string status = (item.Value<string>("status") ?? "").ToLowerInvariant();
        string trackedStatus = (item.Value<string>("trackedDownloadStatus") ?? "").ToLowerInvariant();
        if (state == "importblocked")
            return State("import-blocked", "Import blocked", "The download finished, but it could not be imported into the library. A manager needs to check the import problem.");
        if (state is "importpending" or "importing" || status == "completed")
            return State("importing", "Waiting for import", "The download finished and is waiting to be imported. It is not ready to play yet.");
        if (state is "failed" or "failedpending" || status == "failed")
            return State("failed", "Download failed", "The download failed. Check activity with a manager before starting another download.");
        if (status.Contains("paused", StringComparison.Ordinal))
            return State("paused", "Download paused", "The download is paused in the download client. A manager can resume it.");
        if (status.Contains("stalled", StringComparison.Ordinal))
            return State("stalled", "Download stalled", "The download client reports that this download is stalled. A manager can check it or choose another release.");
        if (trackedStatus is "warning" or "error" || !string.IsNullOrWhiteSpace(item.Value<string>("errorMessage")))
            return State("attention", "Download needs attention", "Sonarr/Radarr reports a download problem. Ask a manager to check activity.");
        JObject result = State("downloading", "Downloading", "A release is in the download queue. Wait for it to finish and import.");
        double size = item.Value<double?>("size") ?? 0;
        double left = item.Value<double?>("sizeleft") ?? item.Value<double?>("sizeLeft") ?? size;
        if (size > 0) result["percent"] = Math.Clamp((int)Math.Round(100 * (size - left) / size), 0, 100);
        return result;
    }

    public static JObject State(string key, string label, string reason) => new()
        { ["statusKey"] = key, ["statusLabel"] = label, ["reason"] = reason };

    private static bool TryAirDate(JToken? value, out DateTimeOffset date)
    {
        string? text = value?.Type == JTokenType.Date ? value.ToString(Newtonsoft.Json.Formatting.None).Trim('"') : value?.Value<string>();
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
    }
}
