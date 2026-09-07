using System.Text;
using Jellyfin.Plugin.JellySeerr.Configuration;
using Jellyfin.Plugin.JellySeerr.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public class RequestService
{
    private readonly SeerrApiClient _seerr;
    private readonly UserMappingService _users;
    private readonly QualityCatalogService _quality;
    private readonly ILogger<RequestService> _logger;

    public RequestService(
        SeerrApiClient seerr,
        UserMappingService users,
        QualityCatalogService quality,
        ILogger<RequestService> logger)
    {
        _seerr = seerr;
        _users = users;
        _quality = quality;
        _logger = logger;
    }

    public async Task<RequestOptionsResult> GetRequestOptionsAsync(
        Guid jellyfinUserId,
        string username,
        string mediaType,
        CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        SeerrUserMatch? user = await _users.ResolveAsync(jellyfinUserId, username, cancellationToken).ConfigureAwait(false);
        if (user == null || !user.Mapped)
        {
            return RequestOptionsResult.None;
        }

        bool canRequest = UserMappingService.HasRequestPermission(user.Permissions, mediaType, false);
        bool canRequest4k = !config.Disable4k && UserMappingService.HasRequestPermission(user.Permissions, mediaType, true);
        bool canRequestAdvanced = UserMappingService.HasRequestAdvanced(user.Permissions);
        bool expose = config.ExposeProfilesToEveryone || canRequestAdvanced;

        IReadOnlyList<QualityProfileEntry> hd = _quality.GetEnabled(mediaType, false);
        IReadOnlyList<QualityProfileEntry> uhd = canRequest4k ? _quality.GetEnabled(mediaType, true) : Array.Empty<QualityProfileEntry>();
        JArray options = expose
            ? _quality.ToRequestOptions(hd.Concat(uhd))
            : new JArray();

        QualityProfileEntry? defaultHd = _quality.ResolveDefault(mediaType, false, false);
        QualityProfileEntry? defaultUhd = canRequest4k ? _quality.ResolveDefault(mediaType, true, false) : null;
        QualityProfileEntry? defaultAnime = _quality.ResolveDefault(mediaType, false, true);

        JToken? serviceDetail = await LoadServiceExtrasAsync(mediaType, defaultHd?.ServerId ?? hd.FirstOrDefault()?.ServerId, user.Id, cancellationToken)
            .ConfigureAwait(false);

        return new RequestOptionsResult
        {
            CanRequest = canRequest,
            CanRequest4k = canRequest4k,
            CanRequestAdvanced = canRequestAdvanced || config.ExposeProfilesToEveryone,
            Mapped = true,
            Options = options,
            DefaultProfileId = defaultHd?.ProfileId,
            DefaultServerId = defaultHd?.ServerId,
            Default4kProfileId = defaultUhd?.ProfileId,
            Default4kServerId = defaultUhd?.ServerId,
            DefaultAnimeProfileId = defaultAnime?.ProfileId,
            DefaultAnimeServerId = defaultAnime?.ServerId,
            RootFolders = serviceDetail?["rootFolders"] as JArray ?? new JArray(),
            Tags = serviceDetail?["tags"] as JArray ?? new JArray()
        };
    }

    public async Task<(int StatusCode, string Body, string ContentType)> SubmitAsync(
        Guid jellyfinUserId,
        string username,
        RequestPayload payload,
        CancellationToken cancellationToken)
    {
        SeerrUserMatch? user = await _users.ResolveAsync(jellyfinUserId, username, cancellationToken).ConfigureAwait(false);
        if (user == null || !user.Mapped)
        {
            return (400, "{\"message\":\"Could not match this Jellyfin user to a Seerr user.\"}", "application/json");
        }

        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        if (payload.Is4k && config.Disable4k)
        {
            return (403, "{\"message\":\"4K requests are disabled in JellySeerr.\"}", "application/json");
        }

        if (!UserMappingService.HasRequestPermission(user.Permissions, payload.MediaType, payload.Is4k))
        {
            string kind = payload.Is4k ? "4K " : string.Empty;
            string mediaLabel = payload.MediaType == "tv" ? "series" : "movie";
            return (403, $"{{\"message\":\"You do not have permission to make {kind}{mediaLabel} requests.\"}}", "application/json");
        }

        ApplyProfileDefaults(payload, config);
        if (payload.ServerId != null && payload.ProfileId != null &&
            !_quality.IsAllowed(payload.ServerId.Value, payload.ProfileId.Value, payload.MediaType, payload.Is4k))
        {
            return (403, "{\"message\":\"That quality profile is not enabled in JellySeerr.\"}", "application/json");
        }

        JObject body = BuildRequestBody(payload);
        return await _seerr
            .SendAsync(HttpMethod.Post, "/api/v1/request", body.ToString(), user.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(int StatusCode, string Body, string ContentType)> UpdateAsync(
        Guid jellyfinUserId,
        string username,
        int requestId,
        RequestPayload payload,
        CancellationToken cancellationToken)
    {
        SeerrUserMatch? user = await RequireUserAsync(jellyfinUserId, username, cancellationToken).ConfigureAwait(false);
        if (user == null)
        {
            return Unmapped();
        }

        ApplyProfileDefaults(payload, JellySeerrPlugin.Instance.Configuration);
        if (payload.ServerId != null && payload.ProfileId != null &&
            !_quality.IsAllowed(payload.ServerId.Value, payload.ProfileId.Value, payload.MediaType, payload.Is4k))
        {
            return (403, "{\"message\":\"That quality profile is not enabled in JellySeerr.\"}", "application/json");
        }

        JObject body = BuildRequestBody(payload, includeMedia: false);
        return await _seerr
            .SendAsync(HttpMethod.Put, $"/api/v1/request/{requestId}", body.ToString(), user.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<(int StatusCode, string Body, string ContentType)> CancelAsync(
        Guid jellyfinUserId,
        string username,
        int requestId,
        CancellationToken cancellationToken) =>
        SendActionAsync(jellyfinUserId, username, HttpMethod.Delete, $"/api/v1/request/{requestId}", null, cancellationToken);

    public Task<(int StatusCode, string Body, string ContentType)> ApproveAsync(
        Guid jellyfinUserId,
        string username,
        int requestId,
        CancellationToken cancellationToken) =>
        SendActionAsync(jellyfinUserId, username, HttpMethod.Post, $"/api/v1/request/{requestId}/approve", "{}", cancellationToken);

    public Task<(int StatusCode, string Body, string ContentType)> DeclineAsync(
        Guid jellyfinUserId,
        string username,
        int requestId,
        CancellationToken cancellationToken) =>
        SendActionAsync(jellyfinUserId, username, HttpMethod.Post, $"/api/v1/request/{requestId}/decline", "{}", cancellationToken);

    public Task<(int StatusCode, string Body, string ContentType)> RetryAsync(
        Guid jellyfinUserId,
        string username,
        int requestId,
        CancellationToken cancellationToken) =>
        SendActionAsync(jellyfinUserId, username, HttpMethod.Post, $"/api/v1/request/{requestId}/retry", "{}", cancellationToken);

    public async Task<(int StatusCode, string Body, string ContentType)> BulkCancelAsync(
        Guid jellyfinUserId,
        string username,
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        int cancelled = 0;
        var errors = new List<object>();
        foreach (int id in ids.Distinct())
        {
            (int status, string body, _) = await CancelAsync(jellyfinUserId, username, id, cancellationToken)
                .ConfigureAwait(false);
            if (status is >= 200 and < 300)
            {
                cancelled++;
            }
            else
            {
                errors.Add(new { id, status, message = TryMessage(body) });
            }
        }

        JObject result = new()
        {
            ["cancelled"] = cancelled,
            ["errors"] = JArray.FromObject(errors)
        };
        return (200, result.ToString(), "application/json");
    }

    public async Task<(int StatusCode, string Body, string ContentType)> QuotaAsync(
        Guid jellyfinUserId,
        string username,
        CancellationToken cancellationToken)
    {
        SeerrUserMatch? user = await _users.ResolveAsync(jellyfinUserId, username, cancellationToken).ConfigureAwait(false);
        if (user == null || !user.Mapped)
        {
            return Unmapped();
        }

        return await _seerr
            .SendAsync(HttpMethod.Get, $"/api/v1/user/{user.Id}/quota", null, user.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(int StatusCode, string Body, string ContentType)> WatchlistAsync(
        Guid jellyfinUserId,
        string username,
        HttpMethod method,
        int tmdbId,
        string mediaType,
        string? title,
        CancellationToken cancellationToken)
    {
        SeerrUserMatch? user = await _users.ResolveAsync(jellyfinUserId, username, cancellationToken).ConfigureAwait(false);
        if (user == null || !user.Mapped)
        {
            return Unmapped();
        }

        if (method == HttpMethod.Delete)
        {
            return await _seerr
                .SendAsync(HttpMethod.Delete, $"/api/v1/watchlist/{tmdbId}?mediaType={Uri.EscapeDataString(mediaType)}", null, user.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        JObject body = new()
        {
            ["tmdbId"] = tmdbId,
            ["mediaType"] = mediaType
        };
        if (!string.IsNullOrWhiteSpace(title))
        {
            body["title"] = title;
        }

        return await _seerr
            .SendAsync(HttpMethod.Post, "/api/v1/watchlist", body.ToString(), user.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(int StatusCode, string Body, string ContentType)> CreateIssueAsync(
        Guid jellyfinUserId,
        string username,
        IssuePayload payload,
        CancellationToken cancellationToken)
    {
        SeerrUserMatch? user = await _users.ResolveAsync(jellyfinUserId, username, cancellationToken).ConfigureAwait(false);
        if (user == null || !user.Mapped)
        {
            return Unmapped();
        }

        JObject body = new()
        {
            ["issueType"] = payload.IssueType,
            ["message"] = payload.Message ?? string.Empty,
            ["mediaId"] = payload.MediaId,
        };
        return await _seerr
            .SendAsync(HttpMethod.Post, "/api/v1/issue", body.ToString(), user.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(int StatusCode, string Body, string ContentType)> SendActionAsync(
        Guid jellyfinUserId,
        string username,
        HttpMethod method,
        string path,
        string? body,
        CancellationToken cancellationToken)
    {
        SeerrUserMatch? user = await _users.ResolveAsync(jellyfinUserId, username, cancellationToken).ConfigureAwait(false);
        if (user == null || !user.Mapped)
        {
            return Unmapped();
        }

        return await _seerr.SendAsync(method, path, body, user.Id, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SeerrUserMatch?> RequireUserAsync(Guid jellyfinUserId, string username, CancellationToken cancellationToken) =>
        await _users.ResolveAsync(jellyfinUserId, username, cancellationToken).ConfigureAwait(false);

    private static (int, string, string) Unmapped() =>
        (400, "{\"message\":\"Could not match this Jellyfin user to a Seerr user.\"}", "application/json");

    private void ApplyProfileDefaults(RequestPayload payload, PluginConfiguration config)
    {
        bool canPick = config.ExposeProfilesToEveryone;
        if (payload.ServerId == null || payload.ProfileId == null)
        {
            QualityProfileEntry? fallback = _quality.ResolveDefault(payload.MediaType, payload.Is4k, payload.IsAnime);
            if (fallback != null)
            {
                payload.ServerId ??= fallback.ServerId;
                payload.ProfileId ??= fallback.ProfileId;
                payload.RootFolder ??= fallback.DefaultRootFolder;
            }
        }

        _ = canPick;
    }

    private static JObject BuildRequestBody(RequestPayload payload, bool includeMedia = true)
    {
        JObject body = new();
        if (includeMedia)
        {
            body["mediaType"] = payload.MediaType;
            body["mediaId"] = payload.MediaId;
        }

        if (string.Equals(payload.MediaType, "tv", StringComparison.OrdinalIgnoreCase))
        {
            body["seasons"] = payload.Seasons is { Count: > 0 }
                ? new JArray(payload.Seasons)
                : (includeMedia ? JToken.FromObject("all") : new JArray());
        }

        if (payload.ServerId != null)
        {
            body["serverId"] = payload.ServerId.Value;
        }

        if (payload.ProfileId != null)
        {
            body["profileId"] = payload.ProfileId.Value;
        }

        if (!string.IsNullOrWhiteSpace(payload.RootFolder))
        {
            body["rootFolder"] = payload.RootFolder;
        }

        if (payload.Tags is { Count: > 0 })
        {
            body["tags"] = new JArray(payload.Tags);
        }

        if (payload.LanguageProfileId != null)
        {
            body["languageProfileId"] = payload.LanguageProfileId.Value;
        }

        if (payload.Is4k)
        {
            body["is4k"] = true;
        }

        return body;
    }

    private async Task<JObject?> LoadServiceExtrasAsync(string mediaType, int? serverId, int seerrUserId, CancellationToken cancellationToken)
    {
        if (serverId == null)
        {
            return null;
        }

        string type = string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase) ? "sonarr" : "radarr";
        JToken? token = await _seerr
            .GetJsonAsync($"/api/v1/service/{type}/{serverId}", seerrUserId, cancellationToken)
            .ConfigureAwait(false);
        return token as JObject;
    }

    private static string TryMessage(string body)
    {
        try
        {
            return JObject.Parse(body).Value<string>("message") ?? body;
        }
        catch
        {
            return body;
        }
    }
}

public sealed class RequestOptionsResult
{
    public static RequestOptionsResult None { get; } = new();

    public bool CanRequest { get; init; }

    public bool CanRequest4k { get; init; }

    public bool CanRequestAdvanced { get; init; }

    public bool Mapped { get; init; }

    public JArray Options { get; init; } = new();

    public int? DefaultProfileId { get; init; }

    public int? DefaultServerId { get; init; }

    public int? Default4kProfileId { get; init; }

    public int? Default4kServerId { get; init; }

    public int? DefaultAnimeProfileId { get; init; }

    public int? DefaultAnimeServerId { get; init; }

    public JArray RootFolders { get; init; } = new();

    public JArray Tags { get; init; } = new();
}
