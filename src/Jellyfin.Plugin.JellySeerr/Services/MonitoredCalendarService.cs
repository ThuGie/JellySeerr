using System.Globalization;
using Jellyfin.Plugin.JellySeerr.Helpers;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public sealed class MonitoredCalendarService(IHttpClientFactory clients)
{
    private readonly SingleFlight<JObject> _pending = new();
    public async Task<JObject> GetAsync(JArray servers, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        if (start == default || end <= start || end - start > TimeSpan.FromDays(43))
            throw new ReleaseSelectionException(400, "Choose a calendar range of up to 43 days.");
        JArray events = [], waiting = [], failures = [];
        int successful = 0;
        using CancellationTokenSource pageLimit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        pageLimit.CancelAfter(TimeSpan.FromSeconds(40));
        using SemaphoreSlim slots = new(2);
        await Task.WhenAll(servers.OfType<JObject>().Select(async server =>
        {
            string name = server.Value<string>("name") ?? "Sonarr";
            bool entered = false;
            try
            {
                await slots.WaitAsync(pageLimit.Token);
                entered = true;
                using HttpClient client = clients.CreateClient();
                string host = server.Value<string>("hostname") ?? "";
                string apiKey = server.Value<string>("apiKey") ?? "";
                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(apiKey))
                    throw new ReleaseSelectionException(409, "Connection settings are incomplete.");
                client.BaseAddress = new UriBuilder(server.Value<bool?>("useSsl") == true ? "https" : "http", host,
                    server.Value<int?>("port") ?? 8989, (server.Value<string>("baseUrl") ?? "").Trim('/') + "/api/v3/").Uri;
                client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
                client.Timeout = TimeSpan.FromSeconds(30);
                string key = $"monitored-calendar:{ApiCacheScope.ForClient(client)}:{start.UtcTicks}:{end.UtcTicks}";
                JObject data;
                if (JsonMemoryCache.TryGet(key, out JToken? cached)) data = (JObject)cached!;
                else data = await _pending.RunAsync(key, async () =>
                {
                    using HttpClient shared = clients.CreateClient();
                    shared.BaseAddress = client.BaseAddress;
                    shared.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
                    shared.Timeout = TimeSpan.FromSeconds(30);
                    using CancellationTokenSource limit = new(TimeSpan.FromSeconds(35));
                    // The shared lookup finishes independently of a browser navigating away.
                    var seriesTask = Read(shared, "series", limit.Token);
                    string from = Uri.EscapeDataString(start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                    string until = Uri.EscapeDataString(end.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
                    var episodeTask = Read(shared, $"calendar?start={from}&end={until}&unmonitored=false&includeSeries=false", limit.Token);
                    await Task.WhenAll(seriesTask, episodeTask);
                    JObject result = Build(await seriesTask, await episodeTask, start, end, DateTimeOffset.UtcNow);
                    JsonMemoryCache.Set(key, result, TimeSpan.FromMinutes(1));
                    return result;
                }, pageLimit.Token);
                lock (events)
                {
                    successful++;
                    foreach (JObject item in (JArray)data["events"]!) { JObject row = (JObject)item.DeepClone(); row["server"] = name; events.Add(row); }
                    foreach (JObject item in (JArray)data["awaitingDates"]!) { JObject row = (JObject)item.DeepClone(); row["server"] = name; waiting.Add(row); }
                }
            }
            catch (Exception ex) when (ex is ReleaseSelectionException or HttpRequestException or OperationCanceledException or Newtonsoft.Json.JsonException or UriFormatException)
            {
                if (ct.IsCancellationRequested) throw;
                lock (failures) failures.Add(new JObject { ["server"] = name, ["message"] = ex is ReleaseSelectionException e ? e.Message : "Could not load this Sonarr instance. Check its connection from Jellyfin." });
            }
            finally { if (entered) slots.Release(); }
        }));
        if (successful == 0)
            throw new ReleaseSelectionException(502, servers.Count == 0 ? "No Sonarr instance is configured. Connect Sonarr in Seerr or JellySeerr settings."
                : "Could not load the monitored calendar from Sonarr. Check the configured addresses and API keys reachable from Jellyfin.");
        return new JObject { ["events"] = new JArray(events.OrderBy(e => e.Value<DateTimeOffset>("airDateUtc"))),
            ["awaitingDates"] = new JArray(waiting.OrderBy(e => e.Value<string>("title"))), ["failures"] = failures,
            ["serverCount"] = successful, ["message"] = "Monitored episodes from Sonarr. Air dates are estimates and do not guarantee download availability." };
    }

    private static async Task<JArray> Read(HttpClient client, string path, CancellationToken ct)
    {
        using var response = await client.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
            throw new ReleaseSelectionException(502, $"Sonarr {path.Split('?')[0]} returned HTTP {(int)response.StatusCode}. Check the connection and API key.");
        return JArray.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    public static JObject Build(JArray series, JArray episodes, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    {
        var monitored = series.OfType<JObject>().Where(s => s.Value<bool?>("monitored") == true).ToDictionary(s => s.Value<int>("id"));
        JArray events = [];
        foreach (JObject episode in episodes.OfType<JObject>())
        {
            if (!monitored.TryGetValue(episode.Value<int>("seriesId"), out JObject? show) || episode.Value<bool?>("monitored") != true) continue;
            int season = episode.Value<int>("seasonNumber");
            var seasonInfo = (show["seasons"] as JArray)?.OfType<JObject>().FirstOrDefault(s => s.Value<int>("seasonNumber") == season);
            if (seasonInfo?.Value<bool?>("monitored") == false) continue;
            DateTimeOffset? date = Date(episode["airDateUtc"]);
            if (!date.HasValue || date < start || date >= end) continue;
            JObject item = Show(show);
            item["airDateUtc"] = date; item["episodeTitle"] = episode["title"]; item["seasonNumber"] = season;
            item["episodeNumber"] = episode["episodeNumber"]; item["hasFile"] = episode.Value<bool?>("hasFile") == true;
            item["episodeId"] = episode["id"]; item["overview"] = episode["overview"];
            events.Add(item);
        }
        var waiting = monitored.Values.Where(show => show.Value<string>("status") is "continuing" or "upcoming")
            .Where(show => show["seasons"] is not JArray seasons || seasons.Count == 0 || seasons.OfType<JObject>().Any(s => s.Value<bool?>("monitored") == true))
            .Where(show => Date(show["nextAiring"]) is not { } next || next < now)
            .Where(show => !events.OfType<JObject>().Any(e => e.Value<int>("seriesId") == show.Value<int>("id") && Date(e["airDateUtc"]) >= now))
            .Select(Show);
        return new JObject { ["events"] = events, ["awaitingDates"] = new JArray(waiting) };
    }
    private static JObject Show(JObject show) => new() { ["seriesId"] = show["id"], ["tmdbId"] = show["tmdbId"], ["tvdbId"] = show["tvdbId"],
        ["title"] = show["title"], ["network"] = show["network"], ["status"] = show["status"], ["nextAiring"] = show["nextAiring"] };
    private static DateTimeOffset? Date(JToken? value)
    {
        if (value is JValue { Value: DateTimeOffset offset }) return offset;
        if (value is JValue { Value: DateTime instant })
            return new DateTimeOffset(instant.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(instant, DateTimeKind.Utc) : instant);
        return DateTimeOffset.TryParse(value?.ToString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out DateTimeOffset date) ? date : null;
    }
}
