using System.Runtime.Loader;
using System.Xml.Linq;
using Jellyfin.Plugin.JellySeerr.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
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
        return AssemblyLoadContext.All
            .SelectMany(x => x.Assemblies)
            .Any(x => x.FullName?.Contains(".FileTransformation", StringComparison.Ordinal) == true);
    }

    public async Task<object> TestConnectionAsync(CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        if (!_seerr.IsConfigured(config))
        {
            return new { ok = false, message = "Enter a Seerr URL and API key first." };
        }

        try
        {
            JToken? status = await _seerr.GetJsonAsync("/api/v1/status", null, cancellationToken).ConfigureAwait(false);
            JToken? me = await _seerr.GetJsonAsync("/api/v1/auth/me", null, cancellationToken).ConfigureAwait(false);
            if (status == null && me == null)
            {
                return new { ok = false, message = "Seerr did not respond. Check the URL, API key, and that Jellyfin can reach Seerr." };
            }

            string? version = (status as JObject)?.Value<string>("version")
                ?? (me as JObject)?.Value<string>("displayName");
            return new
            {
                ok = true,
                message = string.IsNullOrWhiteSpace(version) ? "Connected to Seerr." : $"Connected to Seerr {version}.",
                version,
                fileTransformation = FileTransformationPresent(),
                profileCount = (config.QualityProfiles ?? new List<QualityProfileEntry>()).Count(p => p.Enabled)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JS • Seerr connection test failed");
            return new { ok = false, message = ex.Message };
        }
    }

    public async Task<object> GetHealthAsync(IUserManager userManager, CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        object test = await TestConnectionAsync(cancellationToken).ConfigureAwait(false);
        List<object> users = _seerr.IsConfigured(config)
            ? await _users.MapJellyfinUsersAsync(userManager, cancellationToken).ConfigureAwait(false)
            : new List<object>();

        return new
        {
            seerrConfigured = _seerr.IsConfigured(config),
            fileTransformation = FileTransformationPresent(),
            seerrFinImportAvailable = File.Exists(SeerrFinConfigPath()),
            enabledProfiles = (config.QualityProfiles ?? new List<QualityProfileEntry>()).Count(p => p.Enabled),
            totalProfiles = (config.QualityProfiles ?? new List<QualityProfileEntry>()).Count,
            test,
            users
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
}
