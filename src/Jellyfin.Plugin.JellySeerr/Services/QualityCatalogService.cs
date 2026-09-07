using Jellyfin.Plugin.JellySeerr.Configuration;
using Jellyfin.Plugin.JellySeerr.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public class QualityCatalogService
{
    private readonly SeerrApiClient _seerr;
    private readonly ILogger<QualityCatalogService> _logger;

    public QualityCatalogService(SeerrApiClient seerr, ILogger<QualityCatalogService> logger)
    {
        _seerr = seerr;
        _logger = logger;
    }

    public async Task<List<QualityProfileEntry>> SyncAsync(CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        var fetched = new List<QualityProfileEntry>();
        fetched.AddRange(await FetchServerTypeAsync("radarr", cancellationToken).ConfigureAwait(false));
        fetched.AddRange(await FetchServerTypeAsync("sonarr", cancellationToken).ConfigureAwait(false));

        Dictionary<string, QualityProfileEntry> existing = (config.QualityProfiles ?? new List<QualityProfileEntry>())
            .ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < fetched.Count; i++)
        {
            QualityProfileEntry next = fetched[i];
            next.SortOrder = i;
            if (existing.TryGetValue(next.Key, out QualityProfileEntry? prior))
            {
                next.Enabled = prior.Enabled;
                next.DisplayName = string.IsNullOrWhiteSpace(prior.DisplayName) ? next.ProfileName : prior.DisplayName;
                next.Description = prior.Description ?? string.Empty;
                next.IsDefaultMovie = prior.IsDefaultMovie;
                next.IsDefaultTv = prior.IsDefaultTv;
                next.IsDefaultMovie4k = prior.IsDefaultMovie4k;
                next.IsDefaultTv4k = prior.IsDefaultTv4k;
                next.IsDefaultAnime = prior.IsDefaultAnime;
                next.SortOrder = prior.SortOrder != 0 ? prior.SortOrder : i;
            }
            else
            {
                next.DisplayName = next.ProfileName;
                next.Enabled = true;
            }
        }

        config.QualityProfiles = fetched.OrderBy(p => p.SortOrder).ThenBy(p => p.DisplayName).ToList();
        EnsureSingleDefaults(config.QualityProfiles);
        JellySeerrPlugin.Instance.SaveConfiguration();
        return config.QualityProfiles;
    }

    public IReadOnlyList<QualityProfileEntry> GetEnabled(string mediaType, bool is4k)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        string serverType = string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase) ? "sonarr" : "radarr";
        IEnumerable<QualityProfileEntry> profiles = (config.QualityProfiles ?? new List<QualityProfileEntry>())
            .Where(p => p.Enabled && string.Equals(p.ServerType, serverType, StringComparison.OrdinalIgnoreCase));

        if (config.Disable4k)
        {
            profiles = profiles.Where(p => !p.Is4k);
        }
        else if (is4k)
        {
            profiles = profiles.Where(p => p.Is4k);
        }
        else
        {
            profiles = profiles.Where(p => !p.Is4k);
        }

        return profiles.OrderBy(p => p.SortOrder).ThenBy(p => p.DisplayName).ToList();
    }

    public QualityProfileEntry? ResolveDefault(string mediaType, bool is4k, bool isAnime)
    {
        IReadOnlyList<QualityProfileEntry> enabled = GetEnabled(mediaType, is4k);
        if (enabled.Count == 0)
        {
            return null;
        }

        bool isTv = string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase);
        QualityProfileEntry? match = null;
        if (isAnime && isTv)
        {
            match = enabled.FirstOrDefault(p => p.IsDefaultAnime) ?? enabled.FirstOrDefault(p => p.IsAnime);
        }

        match ??= is4k
            ? enabled.FirstOrDefault(p => isTv ? p.IsDefaultTv4k : p.IsDefaultMovie4k)
            : enabled.FirstOrDefault(p => isTv ? p.IsDefaultTv : p.IsDefaultMovie);

        return match ?? enabled.FirstOrDefault();
    }

    public bool IsAllowed(int serverId, int profileId, string mediaType, bool is4k)
    {
        return GetEnabled(mediaType, is4k).Any(p => p.ServerId == serverId && p.ProfileId == profileId);
    }

    public QualityProfileEntry? Find(int? serverId, int? profileId)
    {
        if (profileId == null)
        {
            return null;
        }

        IReadOnlyList<QualityProfileEntry> profiles = JellySeerrPlugin.Instance.Configuration.QualityProfiles
            ?? new List<QualityProfileEntry>();
        IEnumerable<QualityProfileEntry> matches = profiles.Where(p => p.ProfileId == profileId.Value);
        if (serverId.HasValue)
        {
            QualityProfileEntry? exact = matches.FirstOrDefault(p => p.ServerId == serverId.Value);
            if (exact != null)
            {
                return exact;
            }
        }

        return matches.FirstOrDefault();
    }

    public string? ResolveProfileName(int? serverId, int? profileId, string? existingName = null)
    {
        if (!string.IsNullOrWhiteSpace(existingName))
        {
            return existingName;
        }

        QualityProfileEntry? profile = Find(serverId, profileId);
        if (profile == null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.ProfileName : profile.DisplayName;
    }

    public void AnnotateRequestProfiles(JObject details)
    {
        JArray? requests = details.Value<JObject>("mediaInfo")?.Value<JArray>("requests");
        if (requests == null)
        {
            return;
        }

        foreach (JObject request in requests.OfType<JObject>())
        {
            string? profileName = ResolveProfileName(
                request.Value<int?>("serverId"),
                request.Value<int?>("profileId"),
                request.Value<string>("profileName"));
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                request["profileName"] = profileName;
            }

            string? qualityLabel = QualityLabelHelper.FromProfile(profileName, request.Value<bool?>("is4k") == true);
            if (!string.IsNullOrWhiteSpace(qualityLabel))
            {
                request["qualityLabel"] = qualityLabel;
            }
        }
    }

    public JArray ToRequestOptions(IEnumerable<QualityProfileEntry> profiles)
    {
        JArray options = new();
        foreach (QualityProfileEntry profile in profiles)
        {
            options.Add(new JObject
            {
                ["serverId"] = profile.ServerId,
                ["serverName"] = profile.ServerName,
                ["is4k"] = profile.Is4k,
                ["isAnime"] = profile.IsAnime,
                ["profileId"] = profile.ProfileId,
                ["profileName"] = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.ProfileName : profile.DisplayName,
                ["originalProfileName"] = profile.ProfileName,
                ["description"] = profile.Description ?? string.Empty,
                ["rootFolder"] = profile.DefaultRootFolder,
                ["isDefaultMovie"] = profile.IsDefaultMovie,
                ["isDefaultTv"] = profile.IsDefaultTv,
                ["isDefaultMovie4k"] = profile.IsDefaultMovie4k,
                ["isDefaultTv4k"] = profile.IsDefaultTv4k,
                ["isDefaultAnime"] = profile.IsDefaultAnime
            });
        }

        return options;
    }

    private async Task<List<QualityProfileEntry>> FetchServerTypeAsync(string serverType, CancellationToken cancellationToken)
    {
        var results = new List<QualityProfileEntry>();
        JToken? listToken = await _seerr.GetJsonAsync($"/api/v1/service/{serverType}", null, cancellationToken)
            .ConfigureAwait(false);
        listToken ??= await _seerr.GetJsonAsync($"/api/v1/settings/{serverType}", null, cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<JObject> servers = listToken switch
        {
            JArray array => array.OfType<JObject>(),
            JObject single => new[] { single },
            _ => Array.Empty<JObject>()
        };

        foreach (JObject server in servers)
        {
            int? serverId = server.Value<int?>("id");
            if (serverId == null)
            {
                continue;
            }

            JToken? detailToken = await _seerr
                .GetJsonAsync($"/api/v1/service/{serverType}/{serverId}", null, cancellationToken)
                .ConfigureAwait(false);
            if (detailToken is not JObject detail)
            {
                _logger.LogWarning("JS • missing {ServerType} detail for {ServerId}", serverType, serverId);
                continue;
            }

            JObject serverDetails = detail.Value<JObject>("server") ?? server;
            JArray? profiles = detail.Value<JArray>("profiles");
            string? defaultRootFolder = serverDetails.Value<string>("activeDirectory")
                ?? detail.Value<JArray>("rootFolders")?.OfType<JObject>().FirstOrDefault()?.Value<string>("path");
            int? defaultProfileId = serverDetails.Value<int?>("activeProfileId");
            bool is4k = serverDetails.Value<bool?>("is4k") ?? server.Value<bool?>("is4k") ?? false;
            bool isAnimeServer = serverDetails.Value<bool?>("isAnime") ?? false;
            string serverName = serverDetails.Value<string>("name") ?? server.Value<string>("name") ?? $"{serverType} {serverId}";

            if (profiles == null)
            {
                continue;
            }

            foreach (JObject profile in profiles.OfType<JObject>())
            {
                int? profileId = profile.Value<int?>("id");
                if (profileId == null)
                {
                    continue;
                }

                string profileName = profile.Value<string>("name") ?? $"Profile {profileId}";
                results.Add(new QualityProfileEntry
                {
                    ServerType = serverType,
                    ServerId = serverId.Value,
                    ServerName = serverName,
                    Is4k = is4k,
                    IsAnime = isAnimeServer || profileName.Contains("anime", StringComparison.OrdinalIgnoreCase),
                    ProfileId = profileId.Value,
                    ProfileName = profileName,
                    DisplayName = profileName,
                    Enabled = true,
                    DefaultRootFolder = defaultRootFolder,
                    IsDefaultMovie = serverType == "radarr" && !is4k && defaultProfileId == profileId,
                    IsDefaultTv = serverType == "sonarr" && !is4k && !isAnimeServer && defaultProfileId == profileId,
                    IsDefaultMovie4k = serverType == "radarr" && is4k && defaultProfileId == profileId,
                    IsDefaultTv4k = serverType == "sonarr" && is4k && defaultProfileId == profileId,
                    IsDefaultAnime = serverType == "sonarr" && isAnimeServer && defaultProfileId == profileId
                });
            }
        }

        return results;
    }

    private static void EnsureSingleDefaults(List<QualityProfileEntry> profiles)
    {
        void KeepOne(Func<QualityProfileEntry, bool> selector, Action<QualityProfileEntry, bool> set)
        {
            bool found = false;
            foreach (QualityProfileEntry profile in profiles)
            {
                if (!selector(profile))
                {
                    continue;
                }

                if (found)
                {
                    set(profile, false);
                }
                else
                {
                    found = true;
                }
            }
        }

        KeepOne(p => p.IsDefaultMovie, (p, v) => p.IsDefaultMovie = v);
        KeepOne(p => p.IsDefaultTv, (p, v) => p.IsDefaultTv = v);
        KeepOne(p => p.IsDefaultMovie4k, (p, v) => p.IsDefaultMovie4k = v);
        KeepOne(p => p.IsDefaultTv4k, (p, v) => p.IsDefaultTv4k = v);
        KeepOne(p => p.IsDefaultAnime, (p, v) => p.IsDefaultAnime = v);
    }
}
