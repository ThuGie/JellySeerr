using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

/// <summary>Bounded, atomic local audit trail. No API keys, URLs, download IDs or filesystem paths.</summary>
public sealed class ReleaseActivityStore
{
    private readonly string _path;
    private readonly ILogger<ReleaseActivityStore> _logger;
    private readonly object _gate = new();
    public ReleaseActivityStore(IApplicationPaths paths, ILogger<ReleaseActivityStore> logger)
        : this(Path.Combine(paths.DataPath, "jellyseerr", "release-activity.json"), logger) { }
    public ReleaseActivityStore(string path, ILogger<ReleaseActivityStore> logger) { _path = path; _logger = logger; }
    private static string Scope(ReleaseContext context) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes($"{context.IsTv}:{context.Request["media"]?["tmdbId"]}:{context.Server["hostname"]}:{context.Server["port"]}:{context.Server["baseUrl"]}")));
    private JArray Load()
    {
        try { return File.Exists(_path) ? JArray.Parse(File.ReadAllText(_path)) : []; }
        catch (Exception ex) when (ex is IOException or Newtonsoft.Json.JsonException or UnauthorizedAccessException)
        { _logger.LogWarning(ex, "JS • release activity could not be read"); return []; }
    }
    public void Add(ReleaseContext context, string action, string outcome, string? release = null, int? season = null, int? episode = null)
    {
        lock (_gate)
        {
            try
            {
                JArray all = Load();
                all.Add(new JObject { ["requestId"] = context.RequestId, ["scope"] = Scope(context), ["date"] = DateTime.UtcNow,
                    ["actorId"] = context.UserId.ToString(), ["actorName"] = context.ActorName[..Math.Min(context.ActorName.Length, 80)], ["action"] = action, ["outcome"] = outcome,
                    ["release"] = (release ?? "")[..Math.Min(release?.Length ?? 0, 400)], ["seasonNumber"] = season, ["episodeNumber"] = episode });
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                string temporary = _path + ".tmp";
                File.WriteAllText(temporary, new JArray(all.TakeLast(2000)).ToString(Newtonsoft.Json.Formatting.None));
                File.Move(temporary, _path, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { _logger.LogWarning(ex, "JS • release action completed but activity could not be saved"); }
        }
    }
    public JArray Read(ReleaseContext context, Guid viewer)
    {
        lock (_gate)
            return new JArray(Load().OfType<JObject>().Where(e => e.Value<int>("requestId") == context.RequestId && e.Value<string>("scope") == Scope(context)).Reverse().Take(100).Select(e => {
                JObject row = (JObject)e.DeepClone(); row["actor"] = row.Value<string>("actorId") == viewer.ToString() ? "You" : row.Value<string>("actorName") is { Length: > 0 } name ? name : "Another user";
                row.Remove("scope"); row.Remove("actorId"); row.Remove("actorName"); return row;
            }));
    }
}
