using Jellyfin.Plugin.JellySeerr.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Controllers;

public partial class JellySeerrController
{
    [HttpGet("jellyseerr-calendar.js")]
    public ActionResult CalendarScript() => ServeEmbedded("Inject.jellyseerr-calendar.js", "application/javascript");

    [HttpGet("jellyseerr-calendar.css")]
    public ActionResult CalendarStyles() => ServeEmbedded("Inject.jellyseerr-calendar.css", "text/css");

    [HttpGet("calendar/monitored")]
    [Authorize]
    public Task<IActionResult> MonitoredCalendar([FromServices] IUserManager users, [FromServices] MonitoredCalendarService calendar,
        [FromQuery] DateTimeOffset start, [FromQuery] DateTimeOffset end, CancellationToken ct) =>
        WithOwnSeerrUser(users, async id =>
        {
            var user = await _userMappingService.ResolveAsync(GetUserId(), GetUsername(users)!, ct);
            var config = JellySeerrPlugin.Instance.Configuration;
            bool allowStandard = user != null && UserMappingService.HasRequestPermission(user.Permissions, "tv", false);
            bool allow4k = user != null && !config.Disable4k && UserMappingService.HasRequestPermission(user.Permissions, "tv", true);
            if (!allowStandard && !allow4k)
                throw new ReleaseSelectionException(403, "TV request permission is required to view the monitored calendar.");
            JArray? configured = await _seerr.GetJsonAsync("/api/v1/settings/sonarr", null, ct) as JArray;
            if (configured == null) throw new ReleaseSelectionException(502, "Could not load the Sonarr connections from Seerr. Check its connection in JellySeerr settings.");
            JArray servers = new(configured.OfType<JObject>().Where(s => s.Value<bool?>("is4k") == true ? allow4k : allowStandard).Select(s => s.DeepClone()));
            if (configured.Count > 0 && servers.Count == 0)
                throw new ReleaseSelectionException(403, "No Sonarr instance is available with your current TV and 4K permissions.");
            foreach (JObject server in servers)
            {
                if (!string.IsNullOrWhiteSpace(config.SonarrApiKey) && server.Value<string>("apiKey") == config.SonarrApiKey
                    && Uri.TryCreate(config.SonarrUrl, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https")
                {
                    server["hostname"] = uri.Host; server["port"] = uri.Port;
                    server["useSsl"] = uri.Scheme == "https"; server["baseUrl"] = uri.AbsolutePath.TrimEnd('/');
                }
            }
            if (configured.Count == 0 && allowStandard && !string.IsNullOrWhiteSpace(config.SonarrApiKey)
                && Uri.TryCreate(config.SonarrUrl, UriKind.Absolute, out Uri? direct) && direct.Scheme is "http" or "https")
                servers.Add(new JObject { ["name"] = "Sonarr", ["hostname"] = direct.Host, ["port"] = direct.Port,
                    ["useSsl"] = direct.Scheme == "https", ["baseUrl"] = direct.AbsolutePath.TrimEnd('/'), ["apiKey"] = config.SonarrApiKey });
            return await calendar.GetAsync(servers, start, end, ct);
        }, ct);
}
