using System.Collections.Concurrent;
using Jellyfin.Plugin.JellySeerr.Helpers;
using Jellyfin.Plugin.JellySeerr.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public partial class ServarrReleaseService
{
    private sealed record SeasonState(JObject Title, JArray Episodes, JArray Files, JArray Grabs)
    {
        public string Signature => string.Join(";", Episodes.OfType<JObject>().OrderBy(e => e.Value<int>("id"))
            .Select(e => $"{e.Value<int>("id")}:{e.Value<int>("episodeNumber")}:{e.Value<int>("episodeFileId")}"));
        public IEnumerable<string> ReleaseTitles => Files.OfType<JObject>().Select(f =>
            ReleaseSelectionPolicy.FindGrab(Grabs, f)?.Value<string>("sourceTitle") ?? f.Value<string>("sceneName") ?? "").Where(t => t.Length > 0);
        public bool CanReject => Files.Count > 0 && Files.OfType<JObject>().All(f => ReleaseSelectionPolicy.FindGrab(Grabs, f) != null);
    }
    private sealed record SeasonSearch(ReleaseContext Context, int SeasonNumber, string Signature, int ProfileId,
        JArray Releases, DateTime Expires);
    private readonly ConcurrentDictionary<string, SeasonSearch> _seasonSearches = new();

    public async Task<JObject> SearchSeasonAsync(ReleaseContext context, int seasonNumber, CancellationToken ct)
    {
        using HttpClient client = CreateClient(context);
        SeasonState state = await LoadSeason(client, context, seasonNumber, ct);
        int seriesId = state.Title.Value<int>("id");
        JArray releases = (JArray)await Read(client, $"release?seriesId={seriesId}&seasonNumber={seasonNumber}", ct);
        JArray rows = [];
        for (int i = 0; i < releases.Count; i++)
        {
            if (releases[i] is not JObject release) continue;
            string scopeReason = ReleaseSelectionPolicy.SeasonScopeReason(release, seriesId, seasonNumber, state.Episodes);
            bool current = state.ReleaseTitles.Contains(release.Value<string>("title") ?? "", StringComparer.OrdinalIgnoreCase);
            bool allowed = scopeReason.Length == 0 && !current
                && ReleaseSelectionPolicy.CanSelect(release, state.Files.Count > 0, context.Allow4k);
            rows.Add(new JObject
            {
                ["index"] = i, ["title"] = release["title"], ["quality"] = release["quality"]?["quality"]?["name"],
                ["size"] = release["size"], ["indexer"] = release["indexer"], ["seeders"] = release["seeders"],
                ["protocol"] = release["protocol"], ["rejections"] = release["rejections"] ?? new JArray(),
                ["current"] = current, ["selectable"] = allowed,
                ["note"] = scopeReason.Length > 0 ? scopeReason : current ? "Current release"
                    : !context.Allow4k && release["quality"]?["quality"]?.Value<int?>("resolution") >= 2160 ? "4K is not permitted for this request."
                    : $"Full season: {state.Episodes.Count} known episodes."
            });
        }
        foreach (var entry in _seasonSearches.Where(p => p.Value.Expires <= DateTime.UtcNow || p.Value.Context.Key == context.Key).ToArray())
            _seasonSearches.TryRemove(entry.Key, out _);
        if (_seasonSearches.Count >= 64) throw new ReleaseSelectionException(429, "Too many active searches. Try again shortly.");
        string token = Guid.NewGuid().ToString("N");
        _seasonSearches[token] = new SeasonSearch(context, seasonNumber, state.Signature, state.Title.Value<int>("qualityProfileId"),
            releases, DateTime.UtcNow.AddMinutes(10));
        return new JObject
        {
            ["searchToken"] = token, ["seasonNumber"] = seasonNumber, ["releases"] = rows,
            ["hasFile"] = state.Files.Count > 0, ["fileCount"] = state.Files.Count,
            ["currentFileInfo"] = new JObject { ["files"] = new JArray(state.Files.OfType<JObject>().Select(FileComparison)) },
            ["currentRelease"] = $"Season {seasonNumber}: {state.Files.Count} existing files, {state.Episodes.Count} known episodes",
            ["affectedEpisodes"] = new JArray(state.Episodes.OfType<JObject>().Where(e => e.Value<int>("episodeFileId") > 0)
                .Select(e => new JObject { ["seasonNumber"] = seasonNumber, ["episodeNumber"] = e["episodeNumber"], ["title"] = e["title"] })),
            ["canRejectCurrent"] = state.CanReject,
            ["message"] = state.Files.Count > 0 && !state.CanReject
                ? "Some original releases could not be identified. You can replace this season without blocklisting its old releases." : ""
        };
    }

    private async Task<JObject> DownloadSeasonAsync(ReleaseContext context, ReleaseDownloadPayload payload, CancellationToken ct)
    {
        await _mutation.WaitAsync(ct);
        try
        {
            if (!_seasonSearches.TryGetValue(payload.SearchToken, out SeasonSearch? search) || search.Expires <= DateTime.UtcNow
                || search.Context.Key != context.Key || search.SeasonNumber != payload.SeasonNumber)
                throw new ReleaseSelectionException(409, "This season search expired or changed. Search again before downloading.");
            if (payload.ReleaseIndex < 0 || payload.ReleaseIndex >= search.Releases.Count)
                throw new ReleaseSelectionException(400, "Select a release from the season search results.");
            if (!JToken.DeepEquals(context.Server, search.Context.Server))
                throw new ReleaseSelectionException(409, "The server settings changed. Search again.");
            using HttpClient client = CreateClient(context);
            SeasonState state = await LoadSeason(client, context, search.SeasonNumber, ct);
            RequireSameSeason(search, state);
            int seriesId = state.Title.Value<int>("id");
            JArray fresh = (JArray)await Read(client, $"release?seriesId={seriesId}&seasonNumber={search.SeasonNumber}", ct);
            JObject selected = (JObject)search.Releases[payload.ReleaseIndex];
            JObject release = fresh.OfType<JObject>().FirstOrDefault(r => r.Value<string>("guid") == selected.Value<string>("guid")
                && r.Value<int?>("indexerId") == selected.Value<int?>("indexerId"))
                ?? throw new ReleaseSelectionException(409, "This season pack is no longer available. Search again.");
            // Searches may take minutes. Recheck every affected file after the search, before mutation.
            state = await LoadSeason(client, context, search.SeasonNumber, ct);
            RequireSameSeason(search, state);
            string scopeReason = ReleaseSelectionPolicy.SeasonScopeReason(release, seriesId, search.SeasonNumber, state.Episodes);
            if (scopeReason.Length > 0) throw new ReleaseSelectionException(403, scopeReason);
            if (!ReleaseSelectionPolicy.CanSelect(release, state.Files.Count > 0, context.Allow4k))
                throw new ReleaseSelectionException(403, "This season pack does not meet the configured download rules.");
            if (state.ReleaseTitles.Contains(release.Value<string>("title") ?? "", StringComparer.OrdinalIgnoreCase))
                throw new ReleaseSelectionException(409, "Choose a different pack from the current releases.");
            if (payload.RejectCurrent && !state.CanReject)
                throw new ReleaseSelectionException(409, "Cannot identify all original releases. Search again or replace without blocklisting.");
            JObject queue = (JObject)await Read(client, "queue?page=1&pageSize=1000", ct);
            HashSet<int> episodeIds = state.Episodes.OfType<JObject>().Select(e => e.Value<int>("id")).ToHashSet();
            if ((queue["records"] as JArray)?.OfType<JObject>().Any(q => episodeIds.Contains(q.Value<int?>("episodeId") ?? -1)
                || q.Value<int?>("seriesId") == seriesId && q.Value<int?>("episodeId") is not > 0
                    && (q.Value<int?>("seasonNumber") == null || q.Value<int?>("seasonNumber") == search.SeasonNumber)) == true)
                throw new ReleaseSelectionException(409, "A download is already queued for this season. Wait for it to finish or resolve it in Sonarr first.");
            _seasonSearches.TryRemove(payload.SearchToken, out _);
            await Send(client, HttpMethod.Post, "release", new JObject
            {
                ["guid"] = release["guid"], ["indexerId"] = release["indexerId"], ["seriesId"] = seriesId
            }, ct);
            logger.LogInformation("JS • user {UserId} queued season {Season} replacement for request {RequestId}, affecting {FileCount} files",
                context.UserId, search.SeasonNumber, context.RequestId, state.Files.Count);
            activity?.Add(context, "Season replacement accepted", payload.RejectCurrent ? "Old releases blocklist requested" : "Queued", release.Value<string>("title"), search.SeasonNumber);
            using CancellationTokenSource cleanup = new(TimeSpan.FromMinutes(2));
            bool failed = false;
            // A season may have many episodes imported from the same pack. Mark that original
            // download only once, and do not select history belonging to the replacement.
            if (payload.RejectCurrent)
            {
                var grabs = state.Files.OfType<JObject>().Select(f => ReleaseSelectionPolicy.FindGrab(state.Grabs, f)!)
                    .GroupBy(g => g.Value<string>("downloadId") is { Length: > 0 } id ? "download:" + id : "history:" + g.Value<int>("id"))
                    .Select(g => g.First());
                foreach (JObject grab in grabs)
                {
                    try { await Send(client, HttpMethod.Post, $"history/failed/{grab.Value<int>("id")}", new JObject(), cleanup.Token); }
                    catch (Exception ex) when (IsCleanupFailure(ex))
                    {
                        failed = true;
                        logger.LogWarning(ex, "JS • season replacement queued but old release blocklisting failed for request {RequestId}", context.RequestId);
                    }
                }
            }
            foreach (JObject file in state.Files.OfType<JObject>())
            {
                try
                {
                    int fileId = file.Value<int>("id");
                    JArray latest = (JArray)await Read(client, $"episode?seriesId={seriesId}", cleanup.Token);
                    var users = latest.OfType<JObject>().Where(e => e.Value<int>("episodeFileId") == fileId).ToArray();
                    if (users.Any(e => e.Value<int>("seasonNumber") != search.SeasonNumber))
                    {
                        failed = true;
                        logger.LogWarning("JS • old file {FileId} is now shared with another season; it was retained", fileId);
                    }
                    if (users.Length > 0 && users.All(e => e.Value<int>("seasonNumber") == search.SeasonNumber))
                        await Send(client, HttpMethod.Delete, $"episodefile/{fileId}", null, cleanup.Token);
                }
                catch (Exception ex) when (IsCleanupFailure(ex))
                {
                    failed = true;
                    logger.LogWarning(ex, "JS • season replacement queued but old file {FileId} removal failed for request {RequestId}", file.Value<int>("id"), context.RequestId);
                }
            }
            activity?.Add(context, "Season cleanup", failed ? "Some cleanup failed; check Sonarr" : "Completed", release.Value<string>("title"), search.SeasonNumber);
            JsonMemoryCache.RemoveByPrefix("servarr:");
            JsonMemoryCache.RemoveByPrefix("requests:");
            return new JObject { ["message"] = failed
                ? "Season replacement queued, but cleanup of some old files or releases could not finish. Check Sonarr before retrying."
                : $"Season {search.SeasonNumber} replacement sent to the download client." };
        }
        finally { _mutation.Release(); }
    }

    private static bool IsCleanupFailure(Exception ex) => ex is HttpRequestException or ReleaseSelectionException
        or OperationCanceledException or Newtonsoft.Json.JsonException;

    private static void RequireSameSeason(SeasonSearch search, SeasonState state)
    {
        if (search.Signature != state.Signature || search.ProfileId != state.Title.Value<int>("qualityProfileId"))
            throw new ReleaseSelectionException(409, "Season episodes, files or profile changed. Search again.");
    }

    private static async Task<SeasonState> LoadSeason(HttpClient client, ReleaseContext context, int season, CancellationToken ct)
    {
        if (!context.IsTv || season < 0) throw new ReleaseSelectionException(400, "Choose a TV season.");
        JObject title = await FindTitle(client, context, ct);
        JArray all = (JArray)await Read(client, $"episode?seriesId={title.Value<int>("id")}", ct);
        JArray approved = await Episodes(client, context, title, ct, all);
        JArray episodes = new(approved.OfType<JObject>().Where(e => e.Value<int>("seasonNumber") == season));
        if (episodes.Count == 0) throw new ReleaseSelectionException(403, "Select an approved season from this request.");
        HashSet<int> fileIds = episodes.OfType<JObject>().Select(e => e.Value<int>("episodeFileId")).Where(id => id > 0).ToHashSet();
        if (all.OfType<JObject>().Any(e => fileIds.Contains(e.Value<int>("episodeFileId")) && e.Value<int>("seasonNumber") != season))
            throw new ReleaseSelectionException(409, "A file is shared with another season. Replace that file through Sonarr.");
        JArray files = new(await BoundedAsync.SelectAsync(fileIds.Order(), 4,
            id => Read(client, $"episodefile/{id}", ct), ct));
        JArray history = files.Count == 0 ? [] : (JArray)await Read(client, $"history/series?seriesId={title.Value<int>("id")}&seasonNumber={season}", ct);
        return new SeasonState(title, episodes, files, history);
    }
}
