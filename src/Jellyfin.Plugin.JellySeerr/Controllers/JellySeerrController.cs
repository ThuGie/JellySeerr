using System.Reflection;
using System.Text;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.JellySeerr.Configuration;
using Jellyfin.Plugin.JellySeerr.Configuration.Advanced;
using Jellyfin.Plugin.JellySeerr.Helpers;
using Jellyfin.Plugin.JellySeerr.Model;
using Jellyfin.Plugin.JellySeerr.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Controllers;

[ApiController]
[Route("JellySeerr")]
public class JellySeerrController : ControllerBase
{
    private readonly DiscoveryService _discoveryService;
    private readonly RequestService _requestService;
    private readonly RequestListService _requestListService;
    private readonly QualityCatalogService _qualityCatalogService;
    private readonly ConnectionService _connectionService;
    private readonly UserMappingService _userMappingService;
    private readonly ImageCacheService _imageCacheService;
    private readonly TmdbBackdropService _tmdbBackdropService;
    private readonly JustWatchQualitiesService _justWatchQualitiesService;
    private readonly ServarrProgressService _servarrProgressService;
    private readonly SeerrApiClient _seerr;

    public JellySeerrController(
        DiscoveryService discoveryService,
        RequestService requestService,
        RequestListService requestListService,
        QualityCatalogService qualityCatalogService,
        ConnectionService connectionService,
        UserMappingService userMappingService,
        ImageCacheService imageCacheService,
        TmdbBackdropService tmdbBackdropService,
        JustWatchQualitiesService justWatchQualitiesService,
        ServarrProgressService servarrProgressService,
        SeerrApiClient seerr)
    {
        _discoveryService = discoveryService;
        _requestService = requestService;
        _requestListService = requestListService;
        _qualityCatalogService = qualityCatalogService;
        _connectionService = connectionService;
        _userMappingService = userMappingService;
        _imageCacheService = imageCacheService;
        _tmdbBackdropService = tmdbBackdropService;
        _justWatchQualitiesService = justWatchQualitiesService;
        _servarrProgressService = servarrProgressService;
        _seerr = seerr;
    }

    private Guid GetUserId()
    {
        string? userIdString = User.Claims
            .FirstOrDefault(x => x.Type.Equals("Jellyfin-UserId", StringComparison.OrdinalIgnoreCase))?.Value;
        return string.IsNullOrEmpty(userIdString) ? Guid.Empty : Guid.Parse(userIdString);
    }

    private string? GetUsername(IUserManager userManager)
    {
        Guid userId = GetUserId();
        return userId == Guid.Empty ? null : userManager.GetUserById(userId)?.Username;
    }

    private void SetCacheHeaders()
    {
        var config = JellySeerrPlugin.Instance.Configuration;
        if (config.DeveloperMode)
        {
            Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        }
        else
        {
            Response.Headers.CacheControl = $"public, max-age={config.CacheTimeoutSeconds}";
        }

        string version = JellySeerrPlugin.Instance.GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        Response.Headers.ETag = $"\"v{version}-c{config.CacheBustCounter}\"";
    }

    [HttpGet("jellyseerr-tabs.js")]
    public ActionResult GetScript() => ServeEmbedded("Inject.jellyseerr-tabs.js", "application/javascript");

    [HttpGet("jellyseerr-tabs.css")]
    public ActionResult GetStylesheet() => ServeEmbedded("Inject.jellyseerr-tabs.css", "text/css");

    [HttpGet("jellyseerr-nativeui.js")]
    public ActionResult GetNativeUiScript() => ServeEmbedded("Inject.jellyseerr-nativeui.js", "application/javascript");

    [HttpGet("jellyseerr-modal.js")]
    public ActionResult GetModalScript() => ServeEmbedded("Inject.jellyseerr-modal.js", "application/javascript");

    [HttpGet("jellyseerr-modal.css")]
    public ActionResult GetModalStylesheet() => ServeEmbedded("Inject.jellyseerr-modal.css", "text/css");

    [HttpGet("jellyseerr-requests.js")]
    public ActionResult GetRequestsScript() => ServeEmbedded("Inject.jellyseerr-requests.js", "application/javascript");

    [HttpGet("jellyseerr-requests.css")]
    public ActionResult GetRequestsStylesheet() => ServeEmbedded("Inject.jellyseerr-requests.css", "text/css");

    [HttpGet("Configuration")]
    [Authorize(Roles = "Administrator")]
    public ActionResult<PluginConfiguration> GetConfiguration() => JellySeerrPlugin.Instance.Configuration;

    [HttpGet("admin/health")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult> GetHealth(CancellationToken cancellationToken) =>
        Ok(await _connectionService.GetHealthAsync(cancellationToken).ConfigureAwait(false));

    [HttpPost("admin/test")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult> TestConnection(CancellationToken cancellationToken) =>
        Ok(await _connectionService.TestConnectionAsync(cancellationToken).ConfigureAwait(false));

    [HttpPost("admin/import-seerrfin")]
    [Authorize(Roles = "Administrator")]
    public ActionResult ImportSeerrFin() => Ok(_connectionService.ImportSeerrFin());

    [HttpPost("admin/sync-quality")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult> SyncQuality(CancellationToken cancellationToken) =>
        Ok(await _qualityCatalogService.SyncAsync(cancellationToken).ConfigureAwait(false));

    [HttpGet("admin/users")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult> GetUsers([FromServices] IUserManager userManager, CancellationToken cancellationToken) =>
        Ok(await _userMappingService.MapJellyfinUsersAsync(userManager, cancellationToken).ConfigureAwait(false));

    [HttpGet("session")]
    [Authorize]
    public async Task<ActionResult> GetSession([FromServices] IUserManager userManager, CancellationToken cancellationToken)
    {
        Guid userId = GetUserId();
        string? username = GetUsername(userManager);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(username))
        {
            return Forbid();
        }

        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        SeerrUserMatch? match = await _userMappingService.ResolveAsync(userId, username, cancellationToken).ConfigureAwait(false);
        bool mapped = match?.Mapped == true;
        bool hide = config.HideUnmappedUsers && !mapped;
        return Ok(new
        {
            mapped,
            hideUnmapped = hide,
            canManage = mapped && UserMappingService.HasManageRequests(match?.Permissions ?? 0),
            canRequestAdvanced = mapped && UserMappingService.HasRequestAdvanced(match?.Permissions ?? 0),
            confirmCancel = config.ConfirmCancel,
            showQuotaWarnings = config.ShowQuotaWarnings,
            enableManagerTools = config.EnableManagerTools,
            disable4k = config.Disable4k,
            exposeProfilesToEveryone = config.ExposeProfilesToEveryone
        });
    }

    [HttpGet("display-settings")]
    [Authorize]
    public async Task<ActionResult> GetDisplaySettings([FromServices] IUserManager userManager, CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        List<TabConfig> tabs = TabConfigHelper.Normalize(config.Tabs);
        Guid userId = GetUserId();
        string? username = GetUsername(userManager);
        SeerrUserMatch? match = userId == Guid.Empty || string.IsNullOrWhiteSpace(username)
            ? null
            : await _userMappingService.ResolveAsync(userId, username, cancellationToken).ConfigureAwait(false);
        bool mapped = match?.Mapped == true;
        bool hide = config.HideUnmappedUsers && !mapped;

        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        return Ok(new
        {
            config.StreamingServiceUseImages,
            config.StudioNetworkUseImages,
            config.GenreUseBackdrops,
            config.DiscoverUsePosters,
            config.ElegantFinFixes,
            config.QualityRecommendations,
            config.AddSeerrResultsInSearch,
            config.NativeCarousels,
            config.NativeGridPages,
            config.NativeSearchResults,
            hideUnmapped = hide,
            mapped,
            displayCustomizations = DisplayCustomizationsHelper.Resolve(config),
            advanced = AdvancedSettingsHelper.BuildFrontendPayload(config),
            tabs = hide
                ? tabs.Select(tab => new { id = tab.Id, enabled = false, title = tab.Title })
                : tabs.Select(tab => new { id = tab.Id, enabled = tab.Enabled, title = tab.Title }),
            tabBarOrder = TabConfigHelper.NormalizeBarOrder(config.TabBarOrder)
        });
    }

    [HttpGet("backdrop/{mediaType}/{tmdbId}")]
    [Authorize]
    public async Task<ActionResult> GetBackdrop(string mediaType, int tmdbId, [FromQuery] bool preferNeutral, CancellationToken cancellationToken)
    {
        var result = await _tmdbBackdropService.GetCachedBackdropAsync(mediaType, tmdbId, preferNeutral, cancellationToken)
            .ConfigureAwait(false);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("CachedImage/{cacheKey}")]
    public ActionResult GetCachedImage(string cacheKey)
    {
        CachedImageFile? file = _imageCacheService.GetCachedImageFile(cacheKey);
        if (file == null)
        {
            return NotFound();
        }

        return PhysicalFile(file.FilePath, file.ContentType);
    }

    [HttpPost("backdrops")]
    [Authorize]
    public async Task<ActionResult> GetBackdrops([FromBody] BackdropBatchRequestDto payload, CancellationToken cancellationToken)
    {
        var result = await _tmdbBackdropService.GetCachedBackdropsAsync(payload.Items ?? new(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(new BackdropBatchResponseDto { Items = result });
    }

    [HttpGet("discover/movies/trending")]
    [Authorize]
    public ActionResult GetTrendingMovies([FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("movie", "/api/v1/discover/trending", userManager, startIndex, limit);

    [HttpGet("discover/movies/popular")]
    [Authorize]
    public ActionResult GetPopularMovies([FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("movie", "/api/v1/discover/movies", userManager, startIndex, limit);

    [HttpGet("discover/movies/top-rated")]
    [Authorize]
    public ActionResult GetTopRatedMovies([FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("movie", "/api/v1/discover/movies?sortBy=voteCount.desc", userManager, startIndex, limit);

    [HttpGet("discover/movies/upcoming")]
    [Authorize]
    public ActionResult GetUpcomingMovies([FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("movie", "/api/v1/discover/movies/upcoming", userManager, startIndex, limit);

    [HttpGet("discover/tv/trending")]
    [Authorize]
    public ActionResult GetTrendingTv([FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("tv", "/api/v1/discover/trending", userManager, startIndex, limit);

    [HttpGet("discover/tv/popular")]
    [Authorize]
    public ActionResult GetPopularTv([FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("tv", "/api/v1/discover/tv", userManager, startIndex, limit);

    [HttpGet("discover/tv/top-rated")]
    [Authorize]
    public ActionResult GetTopRatedTv([FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("tv", "/api/v1/discover/tv?sortBy=voteCount.desc", userManager, startIndex, limit);

    [HttpGet("discover/tv/upcoming")]
    [Authorize]
    public ActionResult GetUpcomingTv([FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("tv", "/api/v1/discover/tv/upcoming", userManager, startIndex, limit);

    [HttpGet("discover/tv/anime")]
    [Authorize]
    public ActionResult GetAnime([FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null)
    {
        string? username = GetUsername(userManager);
        return Ok(_discoveryService.GetAnimeRow(username ?? string.Empty, startIndex, limit));
    }

    [HttpGet("discover/movies/genre/{genreId}")]
    [Authorize]
    public ActionResult GetMovieGenre(int genreId, [FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("movie", $"/api/v1/discover/movies?genre={genreId}", userManager, startIndex, limit);

    [HttpGet("discover/tv/genre/{genreId}")]
    [Authorize]
    public ActionResult GetTvGenre(int genreId, [FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("tv", $"/api/v1/discover/tv?genre={genreId}", userManager, startIndex, limit);

    [HttpGet("discover/movies/studio/{studioId}")]
    [Authorize]
    public ActionResult GetMovieStudio(int studioId, [FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("movie", $"/api/v1/discover/movies/studio/{studioId}", userManager, startIndex, limit);

    [HttpGet("discover/tv/network/{networkId}")]
    [Authorize]
    public ActionResult GetTvNetwork(int networkId, [FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("tv", $"/api/v1/discover/tv/network/{networkId}", userManager, startIndex, limit);

    [HttpGet("discover/movies/provider/{providerId}")]
    [Authorize]
    public ActionResult GetMovieProvider(int providerId, [FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("movie", $"/api/v1/discover/movies/language/en?watchRegion={WatchRegion()}&watchProvider={providerId}", userManager, startIndex, limit);

    [HttpGet("discover/tv/provider/{providerId}")]
    [Authorize]
    public ActionResult GetTvProvider(int providerId, [FromServices] IUserManager userManager, [FromQuery] int startIndex = 0, [FromQuery] int? limit = null) =>
        Discover("tv", $"/api/v1/discover/tv/language/en?watchRegion={WatchRegion()}&watchProvider={providerId}", userManager, startIndex, limit);

    [HttpGet("search")]
    [Authorize]
    public ActionResult Search([FromQuery] string? q, [FromQuery] string? query, [FromServices] IUserManager userManager)
    {
        string? username = GetUsername(userManager);
        return Ok(_discoveryService.Search(username ?? string.Empty, q ?? query ?? string.Empty));
    }

    [HttpGet("genres/movie")]
    [Authorize]
    public ActionResult MovieGenres([FromServices] IUserManager userManager) =>
        Content(_discoveryService.GetGenreSlider("movie", GetUsername(userManager) ?? string.Empty).ToString(), "application/json");

    [HttpGet("genres/tv")]
    [Authorize]
    public ActionResult TvGenres([FromServices] IUserManager userManager) =>
        Content(_discoveryService.GetGenreSlider("tv", GetUsername(userManager) ?? string.Empty).ToString(), "application/json");

    [HttpGet("providers/movie")]
    [Authorize]
    public ActionResult MovieProviders() => Content(_discoveryService.GetMovieStreamingServices().ToString(), "application/json");

    [HttpGet("providers/tv")]
    [Authorize]
    public ActionResult TvProviders() => Content(_discoveryService.GetTvStreamingServices().ToString(), "application/json");

    [HttpGet("studios/movie")]
    [Authorize]
    public ActionResult MovieStudios() => Content(_discoveryService.GetStudios().ToString(), "application/json");

    [HttpGet("networks/tv")]
    [Authorize]
    public ActionResult TvNetworks() => Content(_discoveryService.GetNetworks().ToString(), "application/json");

    [HttpGet("client-settings")]
    [Authorize]
    public async Task<ActionResult> GetClientSettings([FromServices] IUserManager userManager, CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        string? browseUrl = string.IsNullOrWhiteSpace(config.ExternalJellyseerrUrl)
            ? config.JellyseerrUrl
            : config.ExternalJellyseerrUrl;
        Guid userId = GetUserId();
        string? username = GetUsername(userManager);
        SeerrUserMatch? match = userId == Guid.Empty || string.IsNullOrWhiteSpace(username)
            ? null
            : await _userMappingService.ResolveAsync(userId, username, cancellationToken).ConfigureAwait(false);

        bool isAdmin = IsAdministrator(userManager, userId, match);
        bool canOpenLocal = isAdmin && CanOpenLocalServices();
        string browse = canOpenLocal ? (browseUrl?.Trim() ?? string.Empty) : string.Empty;

        return Ok(new
        {
            tmdbApiKey = config.TmdbApiKey?.Trim() ?? string.Empty,
            jellyseerrBrowseUrl = browse,
            radarrUrl = canOpenLocal ? ServarrUrl(config.RadarrUrl, config.RadarrApiKey) : string.Empty,
            sonarrUrl = canOpenLocal ? ServarrUrl(config.SonarrUrl, config.SonarrApiKey) : string.Empty,
            canOpenLocalServices = canOpenLocal,
            isAdmin,
            radarrConfigured = !string.IsNullOrWhiteSpace(ServarrUrl(config.RadarrUrl, config.RadarrApiKey)),
            sonarrConfigured = !string.IsNullOrWhiteSpace(ServarrUrl(config.SonarrUrl, config.SonarrApiKey)),
            confirmCancel = config.ConfirmCancel,
            enableManagerTools = config.EnableManagerTools,
            canManageRequests = match != null && UserMappingService.HasManageRequests(match.Permissions) && config.EnableManagerTools,
            seerrUserId = match?.Id ?? 0,
            showQuotaWarnings = config.ShowQuotaWarnings
        });
    }

    [HttpGet("details/{mediaType}/{mediaId}")]
    [Authorize]
    public ActionResult GetDetails(string mediaType, int mediaId, [FromServices] IUserManager userManager)
    {
        JObject? details = _discoveryService.GetMediaDetails(GetUsername(userManager) ?? string.Empty, mediaType, mediaId);
        return details == null ? NotFound() : Content(details.ToString(), "application/json");
    }

    [HttpGet("library-item/{mediaType}/{tmdbId:int}")]
    [Authorize]
    public ActionResult GetLibraryItem(string mediaType, int tmdbId, [FromServices] IUserManager userManager)
    {
        var user = userManager.GetUserById(GetUserId());
        if (user == null)
        {
            return Forbid();
        }

        if (tmdbId <= 0)
        {
            return BadRequest(new { message = "A TMDB id is required." });
        }

        Guid? itemId = _requestListService.FindLibraryItemId(user, mediaType, tmdbId);
        return itemId.HasValue
            ? Ok(new { id = itemId.Value.ToString("N") })
            : NotFound();
    }

    [HttpGet("justwatch/qualities/{mediaType}/{tmdbId}")]
    [Authorize]
    public async Task<ActionResult> GetJustWatchQualities(string mediaType, int tmdbId, CancellationToken cancellationToken)
    {
        var qualities = await _justWatchQualitiesService.GetQualitiesAsync(mediaType, tmdbId, cancellationToken).ConfigureAwait(false);
        return qualities == null
            ? NotFound()
            : Ok(new { highestReleasedQuality = qualities.HighestReleasedQuality, mostCommonQuality = qualities.MostCommonQuality });
    }

    [HttpGet("request-options/{mediaType}")]
    [Authorize]
    public async Task<ActionResult> GetRequestOptions(string mediaType, [FromServices] IUserManager userManager, CancellationToken cancellationToken)
    {
        Guid userId = GetUserId();
        string? username = GetUsername(userManager);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(username))
        {
            return Forbid();
        }

        RequestOptionsResult result = await _requestService
            .GetRequestOptionsAsync(userId, username, mediaType, cancellationToken)
            .ConfigureAwait(false);
        JObject payload = new()
        {
            ["canRequest"] = result.CanRequest,
            ["canRequest4k"] = result.CanRequest4k,
            ["canRequestAdvanced"] = result.CanRequestAdvanced,
            ["mapped"] = result.Mapped,
            ["options"] = result.Options,
            ["defaultProfileId"] = result.DefaultProfileId,
            ["defaultServerId"] = result.DefaultServerId,
            ["default4kProfileId"] = result.Default4kProfileId,
            ["default4kServerId"] = result.Default4kServerId,
            ["defaultAnimeProfileId"] = result.DefaultAnimeProfileId,
            ["defaultAnimeServerId"] = result.DefaultAnimeServerId,
            ["rootFolders"] = result.RootFolders,
            ["tags"] = result.Tags
        };
        return Content(payload.ToString(Newtonsoft.Json.Formatting.None), "application/json");
    }

    [HttpGet("quota")]
    [Authorize]
    public Task<IActionResult> GetQuota([FromServices] IUserManager userManager, CancellationToken cancellationToken) =>
        ProxyUser(userManager, (id, name) => _requestService.QuotaAsync(id, name, cancellationToken));

    [HttpGet("requests")]
    [Authorize]
    public async Task<ActionResult> GetRequests(
        [FromServices] IUserManager userManager,
        [FromQuery] int take = 20,
        [FromQuery] int skip = 0,
        [FromQuery] string? filter = null,
        [FromQuery] bool all = false,
        CancellationToken cancellationToken = default)
    {
        Guid userId = GetUserId();
        string? username = GetUsername(userManager);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(username))
        {
            return Forbid();
        }

        SeerrUserMatch? match = await _userMappingService.ResolveAsync(userId, username, cancellationToken).ConfigureAwait(false);
        bool allowAll = all && match != null && UserMappingService.HasManageRequests(match.Permissions)
            && JellySeerrPlugin.Instance.Configuration.EnableManagerTools;

        (int statusCode, string body) = await _requestListService
            .GetRequestsAsync(userId, username, take, skip, filter, allowAll, CanOpenLocalServices(), cancellationToken)
            .ConfigureAwait(false);
        return new ContentResult { StatusCode = statusCode, Content = body, ContentType = "application/json" };
    }

    [HttpGet("proxy/avatar")]
    [Authorize]
    public async Task<ActionResult> ProxyAvatar([FromQuery] string? path, CancellationToken cancellationToken)
    {
        (byte[]? data, string? contentType) = await _requestListService.GetAvatarAsync(path, cancellationToken).ConfigureAwait(false);
        return data == null || contentType == null ? NotFound() : File(data, contentType);
    }

    [HttpPost("request")]
    [Authorize]
    public Task<IActionResult> CreateRequest([FromServices] IUserManager userManager, [FromBody] RequestPayload payload, CancellationToken cancellationToken) =>
        ProxyUser(userManager, (id, name) => _requestService.SubmitAsync(id, name, payload, cancellationToken));

    [HttpPut("request/{id:int}")]
    [Authorize]
    public Task<IActionResult> UpdateRequest(int id, [FromServices] IUserManager userManager, [FromBody] RequestPayload payload, CancellationToken cancellationToken) =>
        ProxyUser(userManager, (userId, name) => _requestService.UpdateAsync(userId, name, id, payload, cancellationToken));

    [HttpDelete("request/{id:int}")]
    [Authorize]
    public Task<IActionResult> CancelRequest(int id, [FromServices] IUserManager userManager, CancellationToken cancellationToken) =>
        ProxyUser(userManager, (userId, name) => _requestService.CancelAsync(userId, name, id, cancellationToken));

    [HttpPost("request/{id:int}/approve")]
    [Authorize]
    public Task<IActionResult> ApproveRequest(int id, [FromServices] IUserManager userManager, CancellationToken cancellationToken) =>
        ProxyUser(userManager, (userId, name) => _requestService.ApproveAsync(userId, name, id, cancellationToken));

    [HttpPost("request/{id:int}/decline")]
    [Authorize]
    public Task<IActionResult> DeclineRequest(int id, [FromServices] IUserManager userManager, CancellationToken cancellationToken) =>
        ProxyUser(userManager, (userId, name) => _requestService.DeclineAsync(userId, name, id, cancellationToken));

    [HttpPost("request/{id:int}/retry")]
    [Authorize]
    public Task<IActionResult> RetryRequest(int id, [FromServices] IUserManager userManager, CancellationToken cancellationToken) =>
        ProxyUser(userManager, (userId, name) => _requestService.RetryAsync(userId, name, id, cancellationToken));

    [HttpPost("requests/bulk-cancel")]
    [Authorize]
    public Task<IActionResult> BulkCancel([FromServices] IUserManager userManager, [FromBody] BulkCancelPayload payload, CancellationToken cancellationToken) =>
        ProxyUser(userManager, (id, name) => _requestService.BulkCancelAsync(id, name, payload.Ids ?? new List<int>(), cancellationToken));

    [HttpPost("servarr/unmonitor")]
    [Authorize]
    public async Task<IActionResult> Unmonitor(
        [FromServices] IUserManager userManager,
        [FromBody] UnmonitorPayload payload,
        CancellationToken cancellationToken)
    {
        Guid userId = GetUserId();
        string? username = GetUsername(userManager);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(username))
        {
            return Forbid();
        }

        SeerrUserMatch? match = await _userMappingService.ResolveAsync(userId, username, cancellationToken).ConfigureAwait(false);
        if (match == null || !match.Mapped)
        {
            return StatusCode(400, new { message = "Could not match this Jellyfin user to a Seerr user." });
        }

        if (payload == null || payload.MediaId <= 0)
        {
            return BadRequest(new { message = "MediaId is required." });
        }

        if (!UserCanUnmonitor(userManager, match, payload.MediaType, payload.MediaId))
        {
            return StatusCode(403, new { message = "Only the requester or an admin can unmonitor this title." });
        }

        (int status, string body, string contentType) = await _servarrProgressService
            .UnmonitorAsync(payload.MediaType, payload.MediaId, payload.Seasons, cancellationToken)
            .ConfigureAwait(false);
        return new ContentResult { StatusCode = status, Content = body, ContentType = contentType };
    }

    [HttpPost("watchlist")]
    [Authorize]
    public Task<IActionResult> AddWatchlist(
        [FromServices] IUserManager userManager,
        [FromBody] RequestPayload payload,
        CancellationToken cancellationToken) =>
        ProxyUser(userManager, (id, name) => _requestService.WatchlistAsync(id, name, HttpMethod.Post, payload.MediaId, payload.MediaType, null, cancellationToken));

    [HttpDelete("watchlist/{tmdbId:int}")]
    [Authorize]
    public Task<IActionResult> RemoveWatchlist(
        int tmdbId,
        [FromQuery] string mediaType,
        [FromServices] IUserManager userManager,
        CancellationToken cancellationToken) =>
        ProxyUser(userManager, (id, name) => _requestService.WatchlistAsync(id, name, HttpMethod.Delete, tmdbId, mediaType, null, cancellationToken));

    [HttpPost("issue")]
    [Authorize]
    public Task<IActionResult> CreateIssue([FromServices] IUserManager userManager, [FromBody] IssuePayload payload, CancellationToken cancellationToken) =>
        ProxyUser(userManager, (id, name) => _requestService.CreateIssueAsync(id, name, payload, cancellationToken));

    [HttpGet("seerr/{*path}")]
    [Authorize]
    public Task<IActionResult> SeerrProxyGet(string path, [FromServices] IUserManager userManager, CancellationToken cancellationToken) =>
        SeerrProxy(userManager, HttpMethod.Get, path, null, cancellationToken);

    [HttpPost("seerr/{*path}")]
    [Authorize]
    public async Task<IActionResult> SeerrProxyPost(string path, [FromServices] IUserManager userManager, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(Request.Body, Encoding.UTF8);
        string body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        return await SeerrProxy(userManager, HttpMethod.Post, path, body, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IActionResult> SeerrProxy(IUserManager userManager, HttpMethod method, string path, string? body, CancellationToken cancellationToken)
    {
        Guid userId = GetUserId();
        string? username = GetUsername(userManager);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(username))
        {
            return Forbid();
        }

        SeerrUserMatch? match = await _userMappingService.ResolveAsync(userId, username, cancellationToken).ConfigureAwait(false);
        if (match == null || !match.Mapped)
        {
            return StatusCode(400, new { message = "Could not match this Jellyfin user to a Seerr user." });
        }

        (int status, string responseBody, string contentType) = await _seerr
            .SendAsync(method, path, body, match.Id, cancellationToken)
            .ConfigureAwait(false);
        return new ContentResult { StatusCode = status, Content = responseBody, ContentType = contentType };
    }

    private async Task<IActionResult> ProxyUser(IUserManager userManager, Func<Guid, string, Task<(int StatusCode, string Body, string ContentType)>> action)
    {
        Guid userId = GetUserId();
        string? username = GetUsername(userManager);
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(username))
        {
            return Forbid();
        }

        (int status, string body, string contentType) = await action(userId, username).ConfigureAwait(false);
        return new ContentResult { StatusCode = status, Content = body, ContentType = contentType };
    }

    private ActionResult Discover(string mediaType, string path, IUserManager userManager, int startIndex, int? limit)
    {
        string? username = GetUsername(userManager);
        return Ok(_discoveryService.GetDiscoverRow(username ?? string.Empty, path, mediaType, startIndex, limit));
    }

    private static string WatchRegion() =>
        string.IsNullOrWhiteSpace(JellySeerrPlugin.Instance.Configuration.WatchRegion)
            ? "US"
            : JellySeerrPlugin.Instance.Configuration.WatchRegion.Trim();

    private static string ServarrUrl(string? url, string? apiKey) =>
        !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(apiKey) ? url.Trim().TrimEnd('/') : string.Empty;

    private bool IsAdministrator(IUserManager userManager, Guid userId, SeerrUserMatch? match)
    {
        if (match != null && UserMappingService.HasManageRequests(match.Permissions))
        {
            return true;
        }

        var user = userManager.GetUserById(userId);
        return user != null && user.HasPermission(PermissionKind.IsAdministrator);
    }

    private bool UserCanUnmonitor(IUserManager userManager, SeerrUserMatch match, string? mediaType, int tmdbId)
    {
        if (IsAdministrator(userManager, GetUserId(), match))
        {
            return true;
        }

        JObject? details = _discoveryService.GetMediaDetails(GetUsername(userManager) ?? string.Empty, mediaType ?? "movie", tmdbId);
        JObject? mediaInfo = details?.Value<JObject>("mediaInfo") ?? details?.Value<JObject>("media_info");
        JArray? requests = mediaInfo?.Value<JArray>("requests") ?? mediaInfo?.Value<JArray>("Requests");
        if (requests == null)
        {
            return false;
        }

        return requests.OfType<JObject>().Any(req =>
        {
            JObject? requestedBy = req.Value<JObject>("requestedBy") ?? req.Value<JObject>("RequestedBy");
            int? ownerId = requestedBy?.Value<int?>("id") ?? requestedBy?.Value<int?>("Id");
            return ownerId == match.Id;
        });
    }

    private bool CanOpenLocalServices()
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        return LocalNetworkAccessHelper.CanOpenLocalServices(
            HttpContext.Connection.RemoteIpAddress,
            config.JellyseerrUrl,
            config.ExternalJellyseerrUrl,
            config.RadarrUrl,
            config.SonarrUrl);
    }

    private ActionResult ServeEmbedded(string resourceName, string contentType)
    {
        Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"{typeof(JellySeerrPlugin).Namespace}.{resourceName}");
        if (stream == null)
        {
            return NotFound();
        }

        SetCacheHeaders();
        return File(stream, contentType);
    }
}
