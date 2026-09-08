using System.Collections.Concurrent;
using Jellyfin.Plugin.JellySeerr.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public sealed class SeerrUserMatch
{
    public int Id { get; init; }

    public int Permissions { get; init; }

    public string? DisplayName { get; init; }

    public string? Email { get; init; }

    public bool Mapped => Id > 0;
}

public class UserMappingService
{
    public const int PermissionAdmin = 2;
    public const int PermissionManageRequests = 16;
    public const int PermissionRequest = 32;
    public const int PermissionRequest4k = 1024;
    public const int PermissionRequest4kMovie = 2048;
    public const int PermissionRequest4kTv = 4096;
    public const int PermissionRequestAdvanced = 8192;
    public const int PermissionRequestMovie = 262144;
    public const int PermissionRequestTv = 524288;

    private readonly SeerrApiClient _seerr;
    private readonly ILogger<UserMappingService> _logger;
    private readonly ConcurrentDictionary<string, (SeerrUserMatch Match, DateTime CachedAt)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public UserMappingService(SeerrApiClient seerr, ILogger<UserMappingService> logger)
    {
        _seerr = seerr;
        _logger = logger;
    }

    public async Task<SeerrUserMatch?> ResolveAsync(Guid jellyfinUserId, string username, CancellationToken cancellationToken)
    {
        string cacheKey = jellyfinUserId == Guid.Empty ? username : jellyfinUserId.ToString("N");
        if (_cache.TryGetValue(cacheKey, out (SeerrUserMatch Match, DateTime CachedAt) cached) &&
            DateTime.UtcNow - cached.CachedAt < TimeSpan.FromMinutes(5))
        {
            return cached.Match;
        }

        SeerrUserMatch? match = await LookupAsync(jellyfinUserId, username, cancellationToken).ConfigureAwait(false);
        if (match != null)
        {
            _cache[cacheKey] = (match, DateTime.UtcNow);
        }

        return match;
    }

    public void ClearCache() => _cache.Clear();

    public async Task<List<object>> MapJellyfinUsersAsync(IUserManager userManager, CancellationToken cancellationToken)
    {
        var results = new List<object>();
        foreach (var user in userManager.GetUsers())
        {
            SeerrUserMatch? match = null;
            string? error = null;
            try
            {
                match = await LookupAsync(user.Id, user.Username, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                _logger.LogWarning(ex, "JS • failed mapping Jellyfin user {Username}", user.Username);
            }

            results.Add(new
            {
                jellyfinUserId = user.Id,
                jellyfinUsername = user.Username,
                mapped = match?.Mapped == true,
                seerrUserId = match?.Id,
                seerrDisplayName = match?.DisplayName,
                permissions = match?.Permissions ?? 0,
                canRequest = match != null && HasRequestPermission(match.Permissions, "movie", false),
                canRequest4k = match != null && HasRequestPermission(match.Permissions, "movie", true),
                canRequestAdvanced = match != null && HasRequestAdvanced(match.Permissions),
                canManage = match != null && HasManageRequests(match.Permissions),
                error
            });
        }

        return results;
    }

    private async Task<SeerrUserMatch?> LookupAsync(Guid jellyfinUserId, string username, CancellationToken cancellationToken)
    {
        if (!_seerr.IsConfigured() || string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        if (jellyfinUserId != Guid.Empty)
        {
            JToken? byId = await _seerr
                .GetJsonAsync($"/api/v1/user/jellyfin/{jellyfinUserId:D}", null, cancellationToken)
                .ConfigureAwait(false);
            if (byId is JObject idObject && idObject.Value<int?>("id") is int linkedId and > 0)
            {
                return ToMatch(idObject);
            }
        }

        JToken? search = await _seerr
            .GetJsonAsync($"/api/v1/user?q={Uri.EscapeDataString(username)}", null, cancellationToken)
            .ConfigureAwait(false);
        JObject? match = (search as JObject)?.Value<JArray>("results")?
            .OfType<JObject>()
            .FirstOrDefault(x =>
                string.Equals(x.Value<string>("jellyfinUsername"), username, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Value<string>("username"), username, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Value<string>("jellyfinUserId"), jellyfinUserId.ToString("N"), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Value<string>("jellyfinUserId"), jellyfinUserId.ToString("D"), StringComparison.OrdinalIgnoreCase));

        return match == null ? null : ToMatch(match);
    }

    private static SeerrUserMatch ToMatch(JObject user) => new()
    {
        Id = user.Value<int?>("id") ?? 0,
        Permissions = user.Value<int?>("permissions") ?? 0,
        DisplayName = user.Value<string>("displayName") ?? user.Value<string>("username"),
        Email = user.Value<string>("email")
    };

    public static bool HasRequestAdvanced(int permissions) =>
        (permissions & PermissionAdmin) != 0
        || (permissions & PermissionManageRequests) != 0
        || (permissions & PermissionRequestAdvanced) != 0;

    public static bool HasManageRequests(int permissions) =>
        (permissions & PermissionAdmin) != 0 || (permissions & PermissionManageRequests) != 0;

    public static bool HasRequestPermission(int permissions, string mediaType, bool is4k)
    {
        if ((permissions & PermissionAdmin) != 0)
        {
            return true;
        }

        bool isTv = string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase);
        if (is4k)
        {
            return (permissions & PermissionRequest4k) != 0
                || (permissions & (isTv ? PermissionRequest4kTv : PermissionRequest4kMovie)) != 0;
        }

        return (permissions & PermissionRequest) != 0
            || (permissions & (isTv ? PermissionRequestTv : PermissionRequestMovie)) != 0;
    }
}
