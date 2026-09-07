using System.Runtime.Loader;
using System.Xml.Linq;
using Jellyfin.Plugin.JellySeerr.Configuration;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public class ConnectionService
{
    private readonly SeerrApiClient _seerr;
    private readonly UserMappingService _users;
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger<ConnectionService> _logger;

    public ConnectionService(
        SeerrApiClient seerr,
        UserMappingService users,
        IApplicationPaths applicationPaths,
        ILogger<ConnectionService> logger)
    {
        _seerr = seerr;
        _users = users;
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    public bool FileTransformationPresent()
    {
        if (AssemblyLoadContext.All
            .SelectMany(x => x.Assemblies)
            .Any(x =>
            {
                string name = x.GetName().Name ?? x.FullName ?? string.Empty;
                return name.Contains("FileTransformation", StringComparison.OrdinalIgnoreCase);
            }))
        {
            return true;
        }

        try
        {
            string pluginsPath = _applicationPaths.PluginsPath;
            if (Directory.Exists(pluginsPath) &&
                Directory.EnumerateFileSystemEntries(pluginsPath)
                    .Any(path => Path.GetFileName(path).Contains("FileTransformation", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "JS • File Transformation folder probe failed");
        }

        return false;
    }

    public async Task<object> TestConnectionAsync(CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        ConnectionCheckResult seerr = await TestSeerrAsync(config, cancellationToken).ConfigureAwait(false);
        ConnectionCheckResult radarr = await TestArrAsync("Radarr", config.RadarrUrl, config.RadarrApiKey, cancellationToken).ConfigureAwait(false);
        ConnectionCheckResult sonarr = await TestArrAsync("Sonarr", config.SonarrUrl, config.SonarrApiKey, cancellationToken).ConfigureAwait(false);

        List<string> messages = new();
        messages.Add(seerr.Message);
        if (radarr.Configured)
        {
            messages.Add(radarr.Message);
        }

        if (sonarr.Configured)
        {
            messages.Add(sonarr.Message);
        }

        bool ok = seerr.Ok
            && (!radarr.Configured || radarr.Ok)
            && (!sonarr.Configured || sonarr.Ok);

        return new
        {
            ok,
            message = string.Join(" ", messages),
            version = seerr.Version,
            fileTransformation = FileTransformationPresent(),
            profileCount = (config.QualityProfiles ?? new List<QualityProfileEntry>()).Count(p => p.Enabled),
            seerr,
            radarr,
            sonarr
        };
    }

    public async Task<object> GetHealthAsync(CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        object test;
        try
        {
            test = await TestConnectionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JS • health connection test failed");
            test = new { ok = false, message = ex.Message };
        }

        return new
        {
            seerrConfigured = _seerr.IsConfigured(config),
            radarrConfigured = IsArrConfigured(config.RadarrUrl, config.RadarrApiKey),
            sonarrConfigured = IsArrConfigured(config.SonarrUrl, config.SonarrApiKey),
            fileTransformation = FileTransformationPresent(),
            seerrFinImportAvailable = File.Exists(SeerrFinConfigPath()),
            enabledProfiles = (config.QualityProfiles ?? new List<QualityProfileEntry>()).Count(p => p.Enabled),
            totalProfiles = (config.QualityProfiles ?? new List<QualityProfileEntry>()).Count,
            test
        };
    }

    public object? ImportSeerrFin()
    {
        string path = SeerrFinConfigPath();
        if (!File.Exists(path))
        {
            return new { ok = false, message = "SeerrFin settings were not found on this server." };
        }

        try
        {
            XDocument doc = XDocument.Load(path);
            XElement? root = doc.Root;
            if (root == null)
            {
                return new { ok = false, message = "SeerrFin settings file is empty." };
            }

            PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
            config.JellyseerrUrl = Read(root, "JellyseerrUrl") ?? config.JellyseerrUrl;
            config.ExternalJellyseerrUrl = Read(root, "ExternalJellyseerrUrl") ?? config.ExternalJellyseerrUrl;
            config.JellyseerrApiKey = Read(root, "JellyseerrApiKey") ?? config.JellyseerrApiKey;
            config.TmdbApiKey = Read(root, "TmdbApiKey") ?? config.TmdbApiKey;
            config.RadarrUrl = Read(root, "RadarrUrl") ?? config.RadarrUrl;
            config.RadarrApiKey = Read(root, "RadarrApiKey") ?? config.RadarrApiKey;
            config.SonarrUrl = Read(root, "SonarrUrl") ?? config.SonarrUrl;
            config.SonarrApiKey = Read(root, "SonarrApiKey") ?? config.SonarrApiKey;
            config.JellyseerrPreferredLanguages = Read(root, "JellyseerrPreferredLanguages") ?? config.JellyseerrPreferredLanguages;
            config.WatchRegion = Read(root, "WatchRegion") ?? config.WatchRegion;
            JellySeerrPlugin.Instance.SaveConfiguration();
            _users.ClearCache();
            return new { ok = true, message = "Imported Seerr URL, API key, and related keys from SeerrFin." };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JS • SeerrFin import failed");
            return new { ok = false, message = ex.Message };
        }
    }

    private string SeerrFinConfigPath() =>
        Path.Combine(_applicationPaths.PluginConfigurationsPath, "Jellyfin.Plugin.SeerrFin.xml");

    private static string? Read(XElement root, string name)
    {
        string? value = root.Element(name)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<ConnectionCheckResult> TestSeerrAsync(PluginConfiguration config, CancellationToken cancellationToken)
    {
        if (!_seerr.IsConfigured(config))
        {
            return new ConnectionCheckResult(false, false, "Enter a Seerr URL and API key first.");
        }

        try
        {
            JToken? status = await _seerr.GetJsonAsync("/api/v1/status", null, cancellationToken).ConfigureAwait(false);
            JToken? me = await _seerr.GetJsonAsync("/api/v1/auth/me", null, cancellationToken).ConfigureAwait(false);
            if (status == null && me == null)
            {
                return new ConnectionCheckResult(true, false, "Seerr did not respond. Check the URL, API key, and that Jellyfin can reach Seerr.");
            }

            string? version = (status as JObject)?.Value<string>("version")
                ?? (me as JObject)?.Value<string>("displayName");
            return new ConnectionCheckResult(
                true,
                true,
                string.IsNullOrWhiteSpace(version) ? "Connected to Seerr." : $"Connected to Seerr {version}.",
                version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JS • Seerr connection test failed");
            return new ConnectionCheckResult(true, false, "Seerr: " + ex.Message);
        }
    }

    private async Task<ConnectionCheckResult> TestArrAsync(string name, string? url, string? apiKey, CancellationToken cancellationToken)
    {
        if (!IsArrConfigured(url, apiKey))
        {
            return new ConnectionCheckResult(false, false, $"{name} is not configured.");
        }

        try
        {
            using HttpClient client = new()
            {
                BaseAddress = new Uri(url!.Trim().TrimEnd('/') + "/api/v3/"),
                Timeout = TimeSpan.FromSeconds(8)
            };
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey!.Trim());
            using HttpResponseMessage response = await client.GetAsync("system/status", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new ConnectionCheckResult(true, false, $"{name} returned {(int)response.StatusCode}.");
            }

            string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            JObject json = JObject.Parse(raw);
            string? version = json.Value<string>("version") ?? json.Value<string>("instanceName");
            return new ConnectionCheckResult(
                true,
                true,
                string.IsNullOrWhiteSpace(version) ? $"Connected to {name}." : $"Connected to {name} {version}.",
                version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JS • {Name} connection test failed", name);
            return new ConnectionCheckResult(true, false, $"{name}: {ex.Message}");
        }
    }

    private static bool IsArrConfigured(string? url, string? apiKey) =>
        !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(apiKey);
}

public sealed record ConnectionCheckResult(bool Configured, bool Ok, string Message, string? Version = null);
