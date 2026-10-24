using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Jellyfin.Plugin.JellySeerr.Helpers;
using Jellyfin.Plugin.JellySeerr.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public sealed record ReleaseContext(Guid UserId, JObject Request, JObject Server, bool Allow4k, bool CanChooseEpisodes = true, string ActorName = "")
{
    public int RequestId => Request.Value<int>("id");
    public bool IsTv => Request.Value<string>("type") == "tv";
    public string Key => $"{UserId}:{RequestId}:{Request.Value<string>("type")}:{Request["media"]?["tmdbId"]}:{Request.Value<bool>("is4k")}:{Server.Value<int>("id")}";
}

public sealed class ReleaseSelectionException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

/// <summary>Only opaque, user-bound search results can be grabbed. No URLs or Arr IDs from the browser are trusted.</summary>
public partial class ServarrReleaseService(IHttpClientFactory clients, ILogger<ServarrReleaseService> logger, ReleaseActivityStore? activity = null)
{
    private sealed record Search(ReleaseContext Context, int EpisodeId, int FileId, int ProfileId,
        JArray Releases, DateTime Expires);
    private readonly ConcurrentDictionary<string, Search> _searches = new();
    private readonly SemaphoreSlim _mutation = new(1, 1);

    public async Task<JObject> OptionsAsync(ReleaseContext context, CancellationToken ct)
    {
        using HttpClient client = CreateClient(context);
        JObject title = await FindTitle(client, context, ct);
        JArray episodes = context.IsTv ? await Episodes(client, context, title, ct) : [];
        return new JObject
        {
            ["title"] = title.Value<string>("title"),
            ["isTv"] = context.IsTv,
            ["canChooseEpisodes"] = context.CanChooseEpisodes,
            ["seasons"] = new JArray(episodes.OfType<JObject>().GroupBy(e => e.Value<int>("seasonNumber")).Select(g => new JObject
            {
                ["seasonNumber"] = g.Key, ["episodeCount"] = g.Count(),
                ["fileCount"] = g.Select(e => e.Value<int>("episodeFileId")).Where(id => id > 0).Distinct().Count()
            })),
            ["episodes"] = new JArray(episodes.OfType<JObject>().Select(e => new JObject
            {
                ["id"] = e["id"], ["seasonNumber"] = e["seasonNumber"],
                ["episodeNumber"] = e["episodeNumber"], ["title"] = e["title"],
                ["hasFile"] = e.Value<int?>("episodeFileId") > 0
            }))
        };
    }

    public async Task<JObject> SearchAsync(ReleaseContext context, int episodeId, CancellationToken ct)
    {
        if (!context.CanChooseEpisodes) throw new ReleaseSelectionException(403, "Only whole-season replacement is available for another user's request.");
        using HttpClient client = CreateClient(context);
        JObject title = await FindTitle(client, context, ct);
        JObject? episode = await SelectEpisode(client, context, title, episodeId, ct);
        JObject? file = await CurrentFile(client, context, title, episode, ct);
        JArray history = file == null ? [] : await History(client, context, title, episodeId, ct);
        JObject? grab = file == null ? null : ReleaseSelectionPolicy.FindGrab(history, file);
        string? currentTitle = grab?.Value<string>("sourceTitle") ?? file?.Value<string>("sceneName");
        JArray releases = (JArray)await Read(client, "release?" + (context.IsTv ? $"episodeId={episodeId}" : $"movieId={title.Value<int>("id")}"), ct);
        JArray rows = [];
        for (int i = 0; i < releases.Count; i++)
        {
            if (releases[i] is not JObject release) continue;
            bool current = !string.IsNullOrEmpty(currentTitle)
                && string.Equals(currentTitle, release.Value<string>("title"), StringComparison.OrdinalIgnoreCase);
            bool scope = !context.IsTv || ReleaseSelectionPolicy.IsSingleEpisode(release, episode!);
            bool allowed = !current && scope && ReleaseSelectionPolicy.CanSelect(release, file != null, context.Allow4k);
            rows.Add(new JObject
            {
                ["index"] = i, ["title"] = release["title"], ["quality"] = release["quality"]?["quality"]?["name"],
                ["size"] = release["size"], ["indexer"] = release["indexer"], ["seeders"] = release["seeders"],
                ["protocol"] = release["protocol"], ["rejections"] = release["rejections"] ?? new JArray(),
                ["current"] = current, ["selectable"] = allowed,
                ["note"] = !scope ? "Only single-episode releases can be selected here."
                    : current ? "Current release"
                    : !context.Allow4k && release["quality"]?["quality"]?.Value<int?>("resolution") >= 2160 ? "4K is not permitted for this request."
                    : allowed && release.Value<bool?>("rejected") == true ? "Available as a replacement for the existing file." : ""
            });
        }
        foreach (var entry in _searches.Where(p => p.Value.Expires <= DateTime.UtcNow).ToArray()) _searches.TryRemove(entry.Key, out _);
        // Keep at most one search per user/request and bound memory even on very busy servers.
        foreach (var entry in _searches.Where(p => p.Value.Context.Key == context.Key).ToArray()) _searches.TryRemove(entry.Key, out _);
        if (_searches.Count >= 64) throw new ReleaseSelectionException(429, "Too many active searches. Try again shortly.");
        string token = Guid.NewGuid().ToString("N");
        _searches[token] = new Search(context, episodeId, file?.Value<int>("id") ?? 0,
            title.Value<int>("qualityProfileId"), releases, DateTime.UtcNow.AddMinutes(10));
        return new JObject
        {
            ["searchToken"] = token, ["releases"] = rows, ["hasFile"] = file != null,
            ["currentFileInfo"] = FileComparison(file),
            ["currentRelease"] = currentTitle ?? (file == null ? "" : "Existing library file"),
            ["canRejectCurrent"] = grab != null,
            ["message"] = file != null && grab == null ? "The original grab could not be identified. You can replace the file, but cannot blocklist its release." : ""
        };
    }

    public async Task<JObject> DownloadAsync(ReleaseContext context, ReleaseDownloadPayload payload, CancellationToken ct)
    {
        if (payload.SeasonNumber.HasValue) return await DownloadSeasonAsync(context, payload, ct);
        if (!context.CanChooseEpisodes) throw new ReleaseSelectionException(403, "Only whole-season replacement is available for another user's request.");
        await _mutation.WaitAsync(ct);
        try
        {
            if (!_searches.TryGetValue(payload.SearchToken, out Search? search) || search.Expires <= DateTime.UtcNow
                || search.Context.Key != context.Key)
                throw new ReleaseSelectionException(409, "This search expired. Search again before downloading.");
            if (payload.ReleaseIndex < 0 || payload.ReleaseIndex >= search.Releases.Count)
                throw new ReleaseSelectionException(400, "Select a release from the search results.");
            using HttpClient client = CreateClient(context);
            JObject title = await FindTitle(client, context, ct);
            JObject? episode = await SelectEpisode(client, context, title, search.EpisodeId, ct);
            JObject? file = await CurrentFile(client, context, title, episode, ct);
            if ((file?.Value<int>("id") ?? 0) != search.FileId || title.Value<int>("qualityProfileId") != search.ProfileId
                || !JToken.DeepEquals(context.Server, search.Context.Server))
                throw new ReleaseSelectionException(409, "The file or server settings changed. Search again.");
            JObject release = (JObject)search.Releases[payload.ReleaseIndex];
            // Refresh the search before mutation: a release may since have been blocklisted or the profile changed.
            JArray fresh = (JArray)await Read(client, "release?" + (context.IsTv ? $"episodeId={search.EpisodeId}" : $"movieId={title.Value<int>("id")}"), ct);
            release = fresh.OfType<JObject>().FirstOrDefault(r => r.Value<string>("guid") == release.Value<string>("guid")
                && r.Value<int?>("indexerId") == release.Value<int?>("indexerId"))
                ?? throw new ReleaseSelectionException(409, "This release is no longer available. Search again.");
            if (!ReleaseSelectionPolicy.CanSelect(release, file != null, context.Allow4k)
                || context.IsTv && !ReleaseSelectionPolicy.IsSingleEpisode(release, episode!))
                throw new ReleaseSelectionException(403, "This release does not meet the configured download rules.");
            JObject? grab = file == null ? null : ReleaseSelectionPolicy.FindGrab(await History(client, context, title, search.EpisodeId, ct), file);
            string? currentTitle = grab?.Value<string>("sourceTitle") ?? file?.Value<string>("sceneName");
            if (!string.IsNullOrEmpty(currentTitle) && string.Equals(currentTitle, release.Value<string>("title"), StringComparison.OrdinalIgnoreCase))
                throw new ReleaseSelectionException(409, "Choose a different release from the current file.");
            if (payload.RejectCurrent && grab == null)
                throw new ReleaseSelectionException(409, "Cannot identify the original release to blocklist. Search again or replace without blocklisting.");
            if (context.IsTv && file != null)
            {
                // A single physical file can contain multiple episodes. Never delete one through this picker.
                JArray all = (JArray)await Read(client, $"episode?seriesId={title.Value<int>("id")}", ct);
                if (all.OfType<JObject>().Count(e => e.Value<int?>("episodeFileId") == search.FileId) != 1)
                    throw new ReleaseSelectionException(409, "This file contains multiple episodes. Replace it through Sonarr.");
            }
            JObject queue = (JObject)await Read(client, "queue?page=1&pageSize=1000", ct);
            bool active = (queue["records"] as JArray)?.OfType<JObject>().Any(q => context.IsTv
                ? q.Value<int?>("episodeId") == search.EpisodeId
                    || q.Value<int?>("seriesId") == title.Value<int>("id") && q.Value<int?>("episodeId") is not > 0
                        && (q.Value<int?>("seasonNumber") == null || q.Value<int?>("seasonNumber") == episode!.Value<int>("seasonNumber"))
                : q.Value<int?>("movieId") == title.Value<int>("id")) == true;
            if (active)
                throw new ReleaseSelectionException(409, "A download is already in the queue for this movie or episode. Wait for it to finish or resolve it in Sonarr/Radarr before choosing another release.");
            // Consume before any mutation: a timeout must not turn a retry into another grab.
            _searches.TryRemove(payload.SearchToken, out _);
            JObject body = new() { ["guid"] = release["guid"], ["indexerId"] = release["indexerId"] };
            body[context.IsTv ? "episodeId" : "movieId"] = context.IsTv ? search.EpisodeId : title.Value<int>("id");
            // Do not delete a library file until the selected replacement was accepted by the download client.
            await Send(client, HttpMethod.Post, "release", body, ct);
            activity?.Add(context, file != null ? "Replacement accepted" : "Download accepted", payload.RejectCurrent ? "Old release blocklist requested" : "Queued", release.Value<string>("title"), episode?.Value<int>("seasonNumber"), episode?.Value<int>("episodeNumber"));
            string message = "Selected release sent to the download client.";
            // Once a grab succeeds, finish cleanup independently of a browser closing the dialog.
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(60));
            try
            {
                // Mark the OLD grabbed history event, never the newly queued release. Leave the old
                // library file in place during this call to avoid a missing-file automatic search.
                if (payload.RejectCurrent)
                    await Send(client, HttpMethod.Post, $"history/failed/{grab!.Value<int>("id")}", new JObject(), cleanup.Token);
                if (file != null)
                {
                    // A download can import quickly. Recheck before deleting so a new file is never removed.
                    JObject latest = (JObject)await Read(client, context.IsTv ? $"episode/{search.EpisodeId}" : $"movie/{title.Value<int>("id")}", cleanup.Token);
                    int latestFileId = latest.Value<int?>(context.IsTv ? "episodeFileId" : "movieFileId") ?? 0;
                    if (latestFileId == search.FileId)
                        await Send(client, HttpMethod.Delete, $"{(context.IsTv ? "episodefile" : "moviefile")}/{search.FileId}", null, cleanup.Token);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or ReleaseSelectionException or OperationCanceledException or Newtonsoft.Json.JsonException)
            {
                logger.LogWarning(ex, "JS • replacement queued but cleanup failed for request {RequestId}, old file {FileId}", context.RequestId, search.FileId);
                message = "Replacement queued, but blocklisting or removal of the old file could not finish. Check Sonarr/Radarr before retrying.";
            }
            activity?.Add(context, "Replacement cleanup", message, release.Value<string>("title"), episode?.Value<int>("seasonNumber"), episode?.Value<int>("episodeNumber"));
            JsonMemoryCache.RemoveByPrefix("servarr:");
            JsonMemoryCache.RemoveByPrefix("requests:");
            return new JObject { ["message"] = message };
        }
        finally { _mutation.Release(); }
    }

    private HttpClient CreateClient(ReleaseContext context)
    {
        JObject server = context.Server;
        string? host = server.Value<string>("hostname");
        string? key = server.Value<string>("apiKey");
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(key))
            throw new ReleaseSelectionException(400, "The request's Sonarr/Radarr connection is incomplete in Seerr settings.");
        var uri = new UriBuilder(server.Value<bool?>("useSsl") == true ? "https" : "http", host,
            server.Value<int?>("port") ?? (context.IsTv ? 8989 : 7878),
            (server.Value<string>("baseUrl") ?? "").Trim('/') + "/api/v3/");
        HttpClient client = clients.CreateClient();
        client.BaseAddress = uri.Uri;
        client.Timeout = Timeout.InfiniteTimeSpan; // Each operation below has its own bounded timeout.
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    private static async Task<JObject> FindTitle(HttpClient client, ReleaseContext context, CancellationToken ct)
    {
        JObject media = context.Request.Value<JObject>("media") ?? throw new ReleaseSelectionException(400, "Request has no media record.");
        int tmdb = media.Value<int>("tmdbId");
        string path = context.IsTv ? "series" : $"movie?tmdbId={tmdb}";
        JArray all = (JArray)await Read(client, path, ct);
        int? tvdb = media.Value<int?>("tvdbId");
        JObject? title = all.OfType<JObject>().FirstOrDefault(t => context.IsTv
            ? tvdb is > 0 ? t.Value<int?>("tvdbId") == tvdb : t.Value<int?>("tmdbId") == tmdb
            : t.Value<int?>("tmdbId") == tmdb);
        return title ?? throw new ReleaseSelectionException(409, "This title is not in Sonarr/Radarr yet. Wait for the approved request to reach the server, then try again.");
    }

    private static async Task<JArray> Episodes(HttpClient client, ReleaseContext context, JObject title, CancellationToken ct, JArray? suppliedEpisodes = null)
    {
        HashSet<int> seasons = (context.Request["seasons"] as JArray)?.OfType<JObject>()
            .Where(s => s.Value<int?>("status") is 2 or 5)
            .Select(s => s.Value<int>("seasonNumber")).ToHashSet() ?? [];
        JArray all = suppliedEpisodes ?? (JArray)await Read(client, $"episode?seriesId={title.Value<int>("id")}", ct);
        return new JArray(all.OfType<JObject>().Where(e => seasons.Contains(e.Value<int>("seasonNumber")))
            .OrderBy(e => e.Value<int>("seasonNumber")).ThenBy(e => e.Value<int>("episodeNumber")));
    }

    private static async Task<JObject?> SelectEpisode(HttpClient client, ReleaseContext context, JObject title, int episodeId, CancellationToken ct)
    {
        if (!context.IsTv)
        {
            if (episodeId != 0) throw new ReleaseSelectionException(400, "Movie searches do not take an episode.");
            return null;
        }
        return (await Episodes(client, context, title, ct)).OfType<JObject>().FirstOrDefault(e => e.Value<int>("id") == episodeId)
            ?? throw new ReleaseSelectionException(403, "Select an episode from an approved season on your request.");
    }

    private static async Task<JObject?> CurrentFile(HttpClient client, ReleaseContext context, JObject title, JObject? episode, CancellationToken ct)
    {
        int id = (context.IsTv ? episode : title)?.Value<int?>(context.IsTv ? "episodeFileId" : "movieFileId") ?? 0;
        return id > 0 ? (JObject)await Read(client, $"{(context.IsTv ? "episodefile" : "moviefile")}/{id}", ct) : null;
    }

    private static async Task<JArray> History(HttpClient client, ReleaseContext context, JObject title, int episodeId, CancellationToken ct)
    {
        if (!context.IsTv) return (JArray)await Read(client, $"history/movie?movieId={title.Value<int>("id")}", ct);
        JObject history = (JObject)await Read(client, $"history?episodeId={episodeId}&page=1&pageSize=250&sortKey=date&sortDirection=descending", ct);
        return history["records"] as JArray ?? [];
    }

    private static async Task<JToken> Read(HttpClient client, string path, CancellationToken ct)
    {
        bool search = path.StartsWith("release?", StringComparison.Ordinal);
        string operation = search ? "release search" : path.Split('?', '/')[0] + " lookup";
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(search ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(1));
        try
        {
            using HttpResponseMessage response = await client.GetAsync(path, limit.Token);
            if (!response.IsSuccessStatusCode)
                throw new ReleaseSelectionException(502, $"Sonarr/Radarr {operation} returned HTTP {(int)response.StatusCode}. "
                    + (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                        ? "Check the API key and server access in Seerr/JellySeerr settings."
                        : "Check the Sonarr/Radarr log for the failed request."));
            return JToken.Parse(await response.Content.ReadAsStringAsync(limit.Token));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ReleaseSelectionException(504, $"Sonarr/Radarr {operation} timed out after {(search ? "15 minutes" : "one minute")}. Check its activity and indexer connections."); }
        catch (HttpRequestException)
        { throw new ReleaseSelectionException(502, $"Could not connect to Sonarr/Radarr for the {operation}. Check the address reachable from Jellyfin and the service connection."); }
    }

    private static async Task Send(HttpClient client, HttpMethod method, string path, JObject? body, CancellationToken ct)
    {
        using HttpRequestMessage request = new(method, path);
        if (body != null) request.Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromMinutes(1));
        using HttpResponseMessage response = await client.SendAsync(request, limit.Token);
        if (!response.IsSuccessStatusCode)
            throw new ReleaseSelectionException(response.StatusCode == HttpStatusCode.NotFound ? 409 : 502,
                "Sonarr/Radarr could not complete the operation. Refresh the search before trying again.");
    }
}
