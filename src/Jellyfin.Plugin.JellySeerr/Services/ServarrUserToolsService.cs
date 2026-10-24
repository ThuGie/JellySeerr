using System.Collections.Concurrent;
using Jellyfin.Plugin.JellySeerr.Helpers;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public partial class ServarrReleaseService
{
    private sealed record Recovery(ReleaseContext Context, int QueueId, string DownloadId, string Title, int? Season, int[] Episodes, DateTime Expires);
    private readonly ConcurrentDictionary<string, Recovery> _recoveries = new();

    public async Task<JObject> ActivityAsync(ReleaseContext context, CancellationToken ct)
    {
        using HttpClient client = CreateClient(context);
        JObject title = await FindTitle(client, context, ct);
        HashSet<int> approved = context.IsTv ? (await Episodes(client, context, title, ct)).OfType<JObject>().Select(e => e.Value<int>("id")).ToHashSet() : [];
        JArray native;
        if (!context.IsTv) native = await History(client, context, title, 0, ct);
        else
        {
            JObject page = (JObject)await Read(client, $"history?seriesId={title.Value<int>("id")}&page=1&pageSize=250&sortKey=date&sortDirection=descending", ct);
            native = page["records"] as JArray ?? [];
        }
        JArray timeline = new(native.OfType<JObject>().Where(e => !context.IsTv || approved.Contains(e.Value<int>("episodeId")))
            .Take(100).Select(e => new JObject { ["date"] = e["date"], ["action"] = e["eventType"], ["release"] = e["sourceTitle"],
                ["actor"] = "Sonarr/Radarr", ["outcome"] = "Download service event" }));
        foreach (JToken row in activity?.Read(context, context.UserId) ?? []) timeline.Add(row);
        return new JObject { ["events"] = new JArray(timeline.OrderByDescending(e => e.Value<DateTime?>("date")).Take(150)),
            ["message"] = "Recent service events and up to 100 plugin actions for this request. Plugin activity is retained across restarts (latest 2000 actions server-wide)." };
    }

    public async Task<JObject> RecoveryOptionsAsync(ReleaseContext context, CancellationToken ct)
    {
        using HttpClient client = CreateClient(context);
        JObject title = await FindTitle(client, context, ct);
        JArray episodes = context.IsTv ? await Episodes(client, context, title, ct) : [];
        JArray queue = await AvailabilityQueue(client, ct);
        foreach (var old in _recoveries.Where(p => p.Value.Expires < DateTime.UtcNow || p.Value.Context.Key == context.Key).ToArray()) _recoveries.TryRemove(old.Key, out _);
        JArray rows = [];
        foreach (JObject item in queue.OfType<JObject>().Where(q => q.Value<int?>(context.IsTv ? "seriesId" : "movieId") == title.Value<int>("id")))
        {
            Recovery? candidate = RecoveryCandidate(context, item, queue, episodes);
            if (candidate == null || rows.OfType<JObject>().Any(r => r.Value<string>("group") == candidate.DownloadId)) continue;
            if (_recoveries.Count >= 128) throw new ReleaseSelectionException(429, "Too many recovery dialogs are open. Try again shortly.");
            string token = Guid.NewGuid().ToString("N"); _recoveries[token] = candidate;
            rows.Add(new JObject { ["token"] = token, ["group"] = candidate.DownloadId, ["title"] = candidate.Title,
                ["seasonNumber"] = candidate.Season, ["episodeCount"] = candidate.Episodes.Length,
                ["episodes"] = new JArray(episodes.OfType<JObject>().Where(e => candidate.Episodes.Contains(e.Value<int>("id"))).Select(e => new JObject {
                    ["seasonNumber"] = e["seasonNumber"], ["episodeNumber"] = e["episodeNumber"], ["title"] = e["title"] })),
                ["scope"] = context.IsTv ? candidate.Season.HasValue ? $"Whole season {candidate.Season}" : $"{candidate.Episodes.Length} episode(s)" : "Movie" });
        }
        foreach (JObject row in rows) row.Remove("group");
        return new JObject { ["downloads"] = rows, ["message"] = "Only failed, stalled or warning downloads within this request are shown. Recovery removes that download and its partial data, blocklists its specific release, and searches again. Other users may be affected by shared season downloads. Library files are retained." };
    }

    private static bool Recoverable(JObject item) => !new[] { "importing", "imported" }.Contains(item.Value<string>("trackedDownloadState"), StringComparer.OrdinalIgnoreCase)
        && new[] { item.Value<string>("status"), item.Value<string>("trackedDownloadStatus"), item.Value<string>("trackedDownloadState") }
        .Any(s => s != null && new[] { "failed", "stalled", "warning", "error" }.Any(word => s.Contains(word, StringComparison.OrdinalIgnoreCase)));

    private static Recovery? RecoveryCandidate(ReleaseContext context, JObject item, JArray queue, JArray episodes)
    {
        string download = item.Value<string>("downloadId") ?? "";
        if (!Recoverable(item) || download.Length == 0 || item.Value<int>("id") <= 0) return null;
        JObject[] group = queue.OfType<JObject>().Where(q => q.Value<string>("downloadId") == download).ToArray();
        if (group.Any(q => new[] { "importing", "imported" }.Contains(q.Value<string>("trackedDownloadState"), StringComparer.OrdinalIgnoreCase))) return null;
        int[] ids = []; int? season = null;
        if (context.IsTv)
        {
            int series = item.Value<int>("seriesId");
            if (group.Any(q => q.Value<int?>("seriesId") != series)) return null;
            var approved = episodes.OfType<JObject>().ToArray();
            ids = group.Select(q => q.Value<int?>("episodeId") ?? 0).Where(id => id > 0).Distinct().ToArray();
            if (group.Any(q => q.Value<int?>("episodeId") is not > 0))
            {
                int[] seasons = group.Select(q => q.Value<int?>("seasonNumber") ?? -1).Distinct().ToArray();
                if (seasons.Length != 1 || seasons[0] < 0) return null;
                season = seasons[0]; ids = approved.Where(e => e.Value<int>("seasonNumber") == season).Select(e => e.Value<int>("id")).ToArray();
            }
            if (ids.Length == 0 || ids.Any(id => !approved.Any(e => e.Value<int>("id") == id))) return null;
            int[] scopeSeasons = approved.Where(e => ids.Contains(e.Value<int>("id"))).Select(e => e.Value<int>("seasonNumber")).Distinct().ToArray();
            if (scopeSeasons.Length != 1) return null;
            if (!context.CanChooseEpisodes)
            {
                int[] wholeSeason = approved.Where(e => e.Value<int>("seasonNumber") == scopeSeasons[0]).Select(e => e.Value<int>("id")).ToArray();
                if (!wholeSeason.Order().SequenceEqual(ids.Order())) return null;
                season = scopeSeasons[0];
            }
        }
        else if (group.Any(q => q.Value<int?>("movieId") != item.Value<int>("movieId"))) return null;
        return new Recovery(context, item.Value<int>("id"), download, item.Value<string>("title") ?? "Download", season, ids, DateTime.UtcNow.AddMinutes(10));
    }

    public async Task<JObject> RecoverAsync(ReleaseContext context, string token, CancellationToken ct)
    {
        await _mutation.WaitAsync(ct);
        try
        {
            if (!_recoveries.TryGetValue(token, out Recovery? saved) || saved.Context.Key != context.Key || saved.Expires <= DateTime.UtcNow
                || !JToken.DeepEquals(saved.Context.Server, context.Server)) throw new ReleaseSelectionException(409, "Recovery selection expired. Refresh the list.");
            using HttpClient client = CreateClient(context);
            JObject title = await FindTitle(client, context, ct);
            JArray episodes = context.IsTv ? await Episodes(client, context, title, ct) : [];
            JArray queue = await AvailabilityQueue(client, ct);
            JObject? item = queue.OfType<JObject>().FirstOrDefault(q => q.Value<int>("id") == saved.QueueId && q.Value<string>("downloadId") == saved.DownloadId
                && q.Value<int?>(context.IsTv ? "seriesId" : "movieId") == title.Value<int>("id"));
            Recovery? fresh = item == null ? null : RecoveryCandidate(context, item, queue, episodes);
            if (fresh == null || fresh.Title != saved.Title || fresh.Season != saved.Season || !fresh.Episodes.Order().SequenceEqual(saved.Episodes.Order()))
                throw new ReleaseSelectionException(409, "Download state or scope changed. Refresh before recovering it.");
            _recoveries.TryRemove(token, out _);
            await Send(client, HttpMethod.Delete, $"queue/{saved.QueueId}?removeFromClient=true&blocklist=true&skipRedownload=true&changeCategory=false", null, ct);
            activity?.Add(context, "Failed download removed and release blocklisted", "Starting a fresh search", saved.Title, saved.Season);
            using CancellationTokenSource finish = new(TimeSpan.FromSeconds(60));
            try
            {
                JObject command = context.IsTv
                    ? saved.Season.HasValue ? new JObject { ["name"] = "SeasonSearch", ["seriesId"] = title["id"], ["seasonNumber"] = saved.Season }
                        : new JObject { ["name"] = "EpisodeSearch", ["episodeIds"] = new JArray(saved.Episodes) }
                    : new JObject { ["name"] = "MoviesSearch", ["movieIds"] = new JArray(title.Value<int>("id")) };
                await Send(client, HttpMethod.Post, "command", command, finish.Token);
            }
            catch (Exception ex) when (IsCleanupFailure(ex))
            {
                activity?.Add(context, "Recovery search", "Failed after removal; start a search manually", saved.Title, saved.Season);
                return new JObject { ["message"] = "The failed download was removed and its release blocklisted, but the new search could not be confirmed. Refresh availability and start a search or use Advanced download." };
            }
            JsonMemoryCache.RemoveByPrefix("servarr:"); JsonMemoryCache.RemoveByPrefix("requests:");
            activity?.Add(context, "Recovery search", "Search accepted", saved.Title, saved.Season);
            return new JObject { ["message"] = "Failed download removed; that specific release was blocklisted. A new search has started. Finding another release is not guaranteed." };
        }
        finally { _mutation.Release(); }
    }
}
