using Jellyfin.Plugin.JellySeerr.Helpers;
using Jellyfin.Plugin.JellySeerr.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Controllers;

public partial class JellySeerrController
{
    [HttpGet("jellyseerr-user-tools.js")]
    public ActionResult UserToolsScript() => ServeEmbedded("Inject.jellyseerr-user-tools.js", "application/javascript");

    private async Task<IActionResult> WithOwnSeerrUser(IUserManager users, Func<int, Task<JObject>> action, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        string? name = GetUsername(users);
        if (GetUserId() == Guid.Empty || string.IsNullOrWhiteSpace(name)) return Forbid();
        var user = await _userMappingService.ResolveAsync(GetUserId(), name, ct);
        if (user?.Mapped != true) return Forbid();
        try { return Content((await action(user.Id)).ToString(Newtonsoft.Json.Formatting.None), "application/json"); }
        catch (ReleaseSelectionException ex) { return StatusCode(ex.Status, new { message = ex.Message }); }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or Newtonsoft.Json.JsonException)
        { return StatusCode(502, new { message = "Seerr could not complete this operation. Try again shortly." }); }
    }

    private async Task<JObject> UserSeerrJson(int userId, string path, CancellationToken ct, JObject? body = null)
    {
        var result = await _seerr.SendAsync(body == null ? HttpMethod.Get : HttpMethod.Post, path, body?.ToString(), userId, ct);
        if (result.StatusCode is < 200 or >= 300)
            throw new ReleaseSelectionException(result.StatusCode, result.StatusCode == 403
                ? "Your Seerr account does not have permission for this action."
                : "Seerr could not complete this operation. Check your account settings and try again.");
        return string.IsNullOrWhiteSpace(result.Body) ? new JObject() : JObject.Parse(result.Body);
    }

    [HttpGet("notifications")]
    [Authorize]
    public Task<IActionResult> Notifications([FromServices] IUserManager users, CancellationToken ct) =>
        WithOwnSeerrUser(users, async id => UserFeaturePolicy.NotificationView(await UserSeerrJson(id, $"user/{id}/settings/notifications", ct)), ct);

    [HttpPost("notifications")]
    [Authorize]
    public Task<IActionResult> SaveNotifications([FromServices] IUserManager users, [FromBody] NotificationPreferences payload, CancellationToken ct) =>
        WithOwnSeerrUser(users, async id =>
        {
            JObject original = await UserSeerrJson(id, $"user/{id}/settings/notifications", ct);
            JObject update = UserFeaturePolicy.MergeNotifications(original, payload);
            await UserSeerrJson(id, $"user/{id}/settings/notifications", ct, update);
            return new JObject { ["message"] = "Notification preferences saved in Seerr." };
        }, ct);

    [HttpGet("issues/{mediaId:int}")]
    [Authorize]
    public Task<IActionResult> Issues(int mediaId, [FromServices] IUserManager users, CancellationToken ct) =>
        WithOwnSeerrUser(users, async id =>
        {
            if (mediaId <= 0) throw new ReleaseSelectionException(400, "Choose a media record.");
            JArray results = [];
            bool more = false;
            for (int skip = 0; skip < 1000; skip += 100)
            {
                JObject page = await UserSeerrJson(id, $"issue?take=100&skip={skip}&sort=modified", ct);
                var batch = page["results"] as JArray ?? [];
                foreach (JObject issue in batch.OfType<JObject>().Where(i => i["media"]?.Value<int>("id") == mediaId))
                    results.Add(new JObject { ["id"] = issue["id"], ["status"] = issue["status"], ["issueType"] = issue["issueType"],
                        ["problemSeason"] = issue["problemSeason"], ["problemEpisode"] = issue["problemEpisode"],
                        ["createdAt"] = issue["createdAt"], ["updatedAt"] = issue["updatedAt"],
                        ["comments"] = new JArray((issue["comments"] as JArray ?? []).OfType<JObject>().Select(c => new JObject {
                            ["message"] = c["message"], ["createdAt"] = c["createdAt"], ["author"] = c["user"]?["displayName"] ?? c["user"]?["username"] })) });
                more = skip + batch.Count < (page["pageInfo"]?.Value<int>("results") ?? skip + batch.Count);
                if (!more || batch.Count == 0) break;
            }
            return new JObject { ["issues"] = results, ["truncated"] = more };
        }, ct);

    [HttpPost("issues/{id:int}/comment")]
    [Authorize]
    public Task<IActionResult> IssueComment(int id, [FromServices] IUserManager users, [FromBody] IssueCommentPayload payload, CancellationToken ct) =>
        WithOwnSeerrUser(users, async userId =>
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(payload.Message) || payload.Message.Length > 4000)
                throw new ReleaseSelectionException(400, "Enter a comment of 1–4000 characters.");
            await UserSeerrJson(userId, $"issue/{id}/comment", ct, new JObject { ["message"] = payload.Message });
            return new JObject { ["message"] = "Comment added." };
        }, ct);

    [HttpGet("calendar")]
    [Authorize]
    public Task<IActionResult> Calendar([FromServices] IUserManager users, [FromQuery] DateTime start, [FromQuery] DateTime end, CancellationToken ct) =>
        WithOwnSeerrUser(users, async id =>
        {
            if (start == default || end <= start || (end - start).TotalDays > 42)
                throw new ReleaseSelectionException(400, "Choose a calendar range of up to 42 days.");
            using var scopedClient = _seerr.CreateClient(seerrUserId: id);
            string scope = ApiCacheScope.ForClient(scopedClient);
            string key = $"user-calendar:{scope}:{id}:{start:yyyyMMdd}:{end:yyyyMMdd}";
            if (JsonMemoryCache.TryGet(key, out JToken? cached)) return (JObject)cached!;
            JArray requests = []; bool truncated = false;
            for (int skip = 0; skip < 1000; skip += 100)
            {
                JObject page = await UserSeerrJson(id, $"request?take=100&skip={skip}&requestedBy={id}&sort=added", ct);
                JArray batch = page["results"] as JArray ?? [];
                foreach (var r in batch.OfType<JObject>().Where(r => r["requestedBy"]?.Value<int>("id") == id && r.Value<int>("status") is 1 or 2 or 5)) requests.Add(r);
                truncated = skip + batch.Count < (page["pageInfo"]?.Value<int>("results") ?? skip + batch.Count);
                if (!truncated || batch.Count == 0) break;
            }
            async Task<JObject> Metadata(string path)
            {
                string metadataKey = $"calendar-metadata:{scope}:{id}:{path}";
                if (JsonMemoryCache.TryGet(metadataKey, out JToken? value)) return (JObject)value!;
                JObject data = await UserSeerrJson(id, path, ct);
                JsonMemoryCache.Set(metadataKey, data, TimeSpan.FromMinutes(15));
                return data;
            }
            JArray events = []; int failed = 0;
            using SemaphoreSlim slots = new(4);
            await Task.WhenAll(requests.OfType<JObject>().GroupBy(r => $"{r["type"]}:{r["media"]?["tmdbId"]}").Select(async group =>
            {
                await slots.WaitAsync(ct);
                try
                {
                    JObject req = group.First(); string type = req.Value<string>("type") ?? "movie";
                    int tmdb = req["media"]?.Value<int>("tmdbId") ?? 0;
                    if (tmdb <= 0 || type is not ("movie" or "tv")) return;
                    JObject detail = await Metadata($"{type}/{tmdb}");
                    string title = detail.Value<string>("title") ?? detail.Value<string>("name") ?? "Untitled";
                    if (type == "movie")
                    {
                        var theatrical = UserFeaturePolicy.CalendarEvent(detail["releaseDate"], start, end, title, type, tmdb, "Theatrical release", req);
                        if (theatrical != null) lock (events) events.Add(theatrical);
                        // Seerr exposes TMDB regional releases under releases.results, rather than separate digital date fields.
                        JObject[] regions = (detail["releases"]?["results"] as JArray ?? []).OfType<JObject>().ToArray();
                        JObject? market = regions.FirstOrDefault(r => r.Value<string>("iso_3166_1") == "US") ?? regions.FirstOrDefault();
                        foreach (int releaseType in new[] { 4, 5 })
                        {
                            JToken? date = (market?["release_dates"] as JArray ?? []).OfType<JObject>()
                                .Where(r => r.Value<int>("type") == releaseType).OrderBy(r => r.Value<string>("release_date")).FirstOrDefault()?["release_date"];
                            var item = UserFeaturePolicy.CalendarEvent(date, start, end, title, type, tmdb,
                                (releaseType == 4 ? "Digital release" : "Physical release") + " (" + market?.Value<string>("iso_3166_1") + ")", req);
                            if (item != null) lock (events) events.Add(item);
                        }
                    }
                    else
                    {
                        int[] seasons = group.SelectMany(r => (r["seasons"] as JArray ?? []).OfType<JObject>()).Select(s => s.Value<int>("seasonNumber")).Distinct().ToArray();
                        foreach (int season in seasons)
                        {
                            JObject? metadata = (detail["seasons"] as JArray)?.OfType<JObject>().FirstOrDefault(s => s.Value<int>("seasonNumber") == season);
                            if (DateTime.TryParse(metadata?.Value<string>("airDate"), out DateTime first) && first >= end) continue;
                            JObject episodeData = await Metadata($"tv/{tmdb}/season/{season}");
                            foreach (JObject episode in (episodeData["episodes"] as JArray ?? []).OfType<JObject>())
                            {
                                var item = UserFeaturePolicy.CalendarEvent(episode["airDate"], start, end, title, type, tmdb,
                                    $"S{season} E{episode.Value<int>("episodeNumber")} · {episode.Value<string>("name")}", req);
                                if (item != null) lock (events) events.Add(item);
                            }
                        }
                    }
                }
                catch (ReleaseSelectionException) { Interlocked.Increment(ref failed); }
                finally { slots.Release(); }
            }));
            JObject response = new() { ["events"] = new JArray(events.OrderBy(e => e.Value<string>("date"))), ["truncated"] = truncated,
                ["incompleteTitles"] = failed, ["message"] = "Your requested titles. Release and air dates are estimates; they do not guarantee a download. Dates without a time use the published calendar day." };
            JsonMemoryCache.Set(key, response, TimeSpan.FromMinutes(1));
            return response;
        }, ct);

    [HttpGet("request/{id:int}/activity")]
    [Authorize]
    public Task<IActionResult> Activity(int id, [FromServices] IUserManager users, [FromServices] ServarrReleaseService releases, CancellationToken ct) =>
        WithReleaseContext(id, users, context => releases.ActivityAsync(context, ct), ct, sharedSeason: true);

    [HttpGet("request/{id:int}/recovery")]
    [Authorize]
    public Task<IActionResult> RecoveryOptions(int id, [FromServices] IUserManager users, [FromServices] ServarrReleaseService releases, CancellationToken ct) =>
        WithReleaseContext(id, users, context => releases.RecoveryOptionsAsync(context, ct), ct, sharedSeason: true);

    [HttpPost("request/{id:int}/recovery")]
    [Authorize]
    public Task<IActionResult> Recover(int id, [FromServices] IUserManager users, [FromServices] ServarrReleaseService releases, [FromBody] RecoveryPayload payload, CancellationToken ct) =>
        WithReleaseContext(id, users, context => releases.RecoverAsync(context, payload.Token, ct), ct, sharedSeason: true);
}

public sealed class IssueCommentPayload { public string Message { get; set; } = ""; }
public sealed class RecoveryPayload { public string Token { get; set; } = ""; }
