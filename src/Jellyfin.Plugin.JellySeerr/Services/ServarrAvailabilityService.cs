using System.Collections.Concurrent;
using Jellyfin.Plugin.JellySeerr.Helpers;
using Jellyfin.Plugin.JellySeerr.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public partial class ServarrReleaseService
{
    private readonly ConcurrentDictionary<string, DateTime> _missingSearches = new();

    public async Task<JObject> AvailabilityAsync(ReleaseContext context, CancellationToken ct)
    {
        int status = context.Request.Value<int>("status");
        JObject? early = status switch
        {
            1 => AvailabilityPolicy.State("pending", "Awaiting approval", "This request needs approval before downloads can start."),
            3 => AvailabilityPolicy.State("declined", "Request declined", "This request was declined. Contact a manager if you need more information."),
            4 => AvailabilityPolicy.State("request-failed", "Request failed", "Seerr could not complete this request. A manager can check the service connection or retry the request."),
            _ => null
        };
        if (early != null)
        {
            early["isTv"] = context.IsTv;
            early["episodes"] = new JArray();
            return early;
        }
        using HttpClient client = CreateClient(context);
        JObject title;
        try { title = await FindTitle(client, context, ct); }
        catch (ReleaseSelectionException ex) when (ex.Status == 409)
        {
            JObject waiting = AvailabilityPolicy.State("waiting-for-service", "Waiting for download service", "The approved request has not reached Sonarr/Radarr yet. Wait briefly, then refresh; if it persists, ask a manager to check the connection.");
            waiting["isTv"] = context.IsTv;
            waiting["episodes"] = new JArray();
            return waiting;
        }
        JArray queue = await AvailabilityQueue(client, ct);
        if (!context.IsTv)
        {
            JObject? q = queue.OfType<JObject>().FirstOrDefault(q => q.Value<int?>("movieId") == title.Value<int>("id"));
            JObject result = title.Value<int?>("movieFileId") is > 0
                ? AvailabilityPolicy.State("available", "Available", "A file is in the library. If playback fails, choose another release.")
                : q != null ? AvailabilityPolicy.Queue(q)
                : title.Value<bool?>("monitored") == false
                    ? AvailabilityPolicy.State("unmonitored", "Monitoring off", "This movie has no library file and monitoring is off in Radarr. Ask a manager to enable it.")
                : title.Value<bool?>("isAvailable") == false
                    ? AvailabilityPolicy.State("unreleased", "Waiting for release", "Radarr does not consider this movie available for download yet.")
                    : AvailabilityPolicy.State("missing", "Waiting for a suitable release", "There is no library file or active download. This does not prove no releases exist; use Advanced download to check candidates and rejection reasons.");
            result["isTv"] = false;
            result["title"] = title["title"];
            return result;
        }
        JArray episodes = await Episodes(client, context, title, ct);
        JArray rows = new(episodes.OfType<JObject>().Select(e => AvailabilityPolicy.Episode(e, queue, DateTimeOffset.UtcNow,
            context.CanChooseEpisodes && title.Value<bool?>("monitored") == true)));
        foreach (JObject row in rows.OfType<JObject>())
        {
            if (row.Value<string>("statusKey") == "missing" && _missingSearches.TryGetValue(
                MissingSearchKey(client, title, row.Value<int>("seasonNumber")), out DateTime at)
                && DateTime.UtcNow - at < TimeSpan.FromMinutes(2))
            {
                row["statusKey"] = "search-requested";
                row["statusLabel"] = "Search requested recently";
                row["reason"] = "A search was recently requested for this season. If a suitable release is found it will enter the queue. Refresh shortly.";
                row["canSearch"] = false;
            }
        }
        return new JObject
        {
            ["isTv"] = true, ["title"] = title["title"], ["statusLabel"] = "Episode availability",
            ["canSearchMissing"] = context.CanChooseEpisodes && title.Value<bool?>("monitored") == true,
            ["canChooseEpisodes"] = context.CanChooseEpisodes,
            ["reason"] = !context.CanChooseEpisodes ? "You can view this shared season. Only its requester or a manager can search individual missing episodes. Whole-season replacement remains available in Advanced download."
                : title.Value<bool?>("monitored") != true ? "Series monitoring is off. Ask a manager to enable it before searching missing episodes."
                : "Availability comes from Sonarr. Missing episodes can be searched without deleting existing files; finding a release is not guaranteed.",
            ["episodes"] = rows
        };
    }

    public async Task<JObject> SearchMissingAsync(ReleaseContext context, MissingEpisodeSearchPayload payload, CancellationToken ct)
    {
        if (!context.IsTv || !context.CanChooseEpisodes)
            throw new ReleaseSelectionException(403, "Only the requester or a manager can search missing episodes on an approved TV request.");
        if (payload.SeasonNumber < 0 || payload.EpisodeIds is { Count: 0 } || payload.EpisodeIds?.Count > 100)
            throw new ReleaseSelectionException(400, "Select a season and between 1 and 100 missing episodes.");
        await _mutation.WaitAsync(ct);
        try
        {
            using HttpClient client = CreateClient(context);
            JObject title = await FindTitle(client, context, ct);
            if (title.Value<bool?>("monitored") != true)
                throw new ReleaseSelectionException(409, "Series monitoring is off. Ask a manager to enable it.");
            JArray episodes = await Episodes(client, context, title, ct);
            var season = episodes.OfType<JObject>().Where(e => e.Value<int>("seasonNumber") == payload.SeasonNumber).ToArray();
            if (season.Length == 0) throw new ReleaseSelectionException(403, "Select an approved season on this request.");
            if (payload.EpisodeIds != null && payload.EpisodeIds.Any(id => !season.Any(e => e.Value<int>("id") == id)))
                throw new ReleaseSelectionException(403, "One of the selected episodes is outside this approved season.");
            JArray queue = await AvailabilityQueue(client, ct);
            int[] ids = season.Where(e => payload.EpisodeIds == null || payload.EpisodeIds.Contains(e.Value<int>("id")))
                .Where(e => AvailabilityPolicy.Episode(e, queue, DateTimeOffset.UtcNow, true).Value<bool>("canSearch"))
                .Select(e => e.Value<int>("id")).Distinct().ToArray();
            if (ids.Length == 0) throw new ReleaseSelectionException(409, "No aired, monitored missing episodes remain outside the download queue. Refresh availability.");
            if (ids.Length > 100) throw new ReleaseSelectionException(400, "Select at most 100 episodes per search.");
            string key = MissingSearchKey(client, title, payload.SeasonNumber);
            foreach (var item in _missingSearches.Where(p => DateTime.UtcNow - p.Value > TimeSpan.FromMinutes(2)).ToArray()) _missingSearches.TryRemove(item.Key, out _);
            if (_missingSearches.ContainsKey(key)) throw new ReleaseSelectionException(429, "A search was recently started for this season. Give Sonarr two minutes before trying again.");
            // Record before dispatch: on a timeout the command may still have been accepted.
            _missingSearches[key] = DateTime.UtcNow;
            await Send(client, HttpMethod.Post, "command", new JObject { ["name"] = "EpisodeSearch", ["episodeIds"] = new JArray(ids) }, ct);
            logger.LogInformation("JS • user {UserId} searched {Count} missing episodes for request {RequestId}", context.UserId, ids.Length, context.RequestId);
            return new JObject { ["searchedEpisodeIds"] = new JArray(ids), ["message"] = $"Search started for {ids.Length} missing episodes. Sonarr will choose suitable releases; downloads are not guaranteed. Refresh availability shortly." };
        }
        finally { _mutation.Release(); }
    }

    private static string MissingSearchKey(HttpClient client, JObject title, int season) =>
        $"{ApiCacheScope.ForClient(client)}:{title.Value<int>("id")}:{season}";

    private static async Task<JArray> AvailabilityQueue(HttpClient client, CancellationToken ct)
    {
        JArray records = [];
        for (int page = 1; page <= 100; page++)
        {
            JObject response = (JObject)await Read(client, $"queue?page={page}&pageSize=250", ct);
            JArray batch = response["records"] as JArray ?? [];
            foreach (JToken item in batch) records.Add(item.DeepClone());
            if (records.Count >= (response.Value<int?>("totalRecords") ?? records.Count)) return records;
            if (batch.Count == 0) break;
        }
        throw new ReleaseSelectionException(502, "The complete download queue could not be checked. Try again shortly.");
    }
}
