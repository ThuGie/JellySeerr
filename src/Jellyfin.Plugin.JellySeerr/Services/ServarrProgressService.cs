using System.Globalization;
using System.Net.Http;
using System.Text;
using Jellyfin.Plugin.JellySeerr.Configuration;
using Jellyfin.Plugin.JellySeerr.Configuration.Advanced;
using Jellyfin.Plugin.JellySeerr.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public sealed class ServarrProgressService
{
    private readonly ILogger<ServarrProgressService> _logger;

    public ServarrProgressService(ILogger<ServarrProgressService> logger)
    {
        _logger = logger;
    }

    public async Task<(int StatusCode, string Body, string ContentType)> UnmonitorAsync(
        string? mediaType,
        int tmdbId,
        IReadOnlyCollection<int>? seasons,
        CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        bool isTv = string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase);
        if (tmdbId <= 0)
        {
            return MessageResult(400, "A TMDB id is required.");
        }

        try
        {
            async Task<(int StatusCode, string Body, string ContentType)?> TryUnmonitorSeriesAsync()
            {
                if (!IsSonarrConfigured(config))
                {
                    return null;
                }

                using HttpClient client = CreateClient(config.SonarrUrl!, config.SonarrApiKey!);
                JObject? series = await FindByTmdbAsync(client, "series", tmdbId, cancellationToken).ConfigureAwait(false);
                if (series == null)
                {
                    return null;
                }

                HashSet<int> seasonNumbers = seasons?.Where(n => n >= 0).ToHashSet() ?? new HashSet<int>();
                bool includeSpecials = AdvancedSettingsHelper.Resolve(config).Servarr.IncludeSpecialsInSeriesProgress;
                JArray? seasonList = series.Value<JArray>("seasons");
                if (seasonList != null)
                {
                    foreach (JObject season in seasonList.OfType<JObject>())
                    {
                        int number = season.Value<int?>("seasonNumber") ?? -1;
                        if (number < 0)
                        {
                            continue;
                        }

                        if (number == 0 && !includeSpecials && (seasonNumbers.Count == 0 || !seasonNumbers.Contains(0)))
                        {
                            continue;
                        }

                        if (seasonNumbers.Count == 0 || seasonNumbers.Contains(number))
                        {
                            season["monitored"] = false;
                        }
                    }
                }

                bool anySeasonMonitored = seasonList?
                    .OfType<JObject>()
                    .Any(s => (s.Value<int?>("seasonNumber") ?? 0) != 0 && s.Value<bool?>("monitored") == true)
                    == true;
                if (seasonNumbers.Count == 0 || !anySeasonMonitored)
                {
                    series["monitored"] = false;
                }

                int? id = series.Value<int?>("id");
                if (!id.HasValue)
                {
                    return MessageResult(500, "Sonarr did not return a series id.");
                }

                if (!await PutJsonAsync(client, $"series/{id.Value}", series, cancellationToken).ConfigureAwait(false))
                {
                    return MessageResult(502, "Sonarr rejected the unmonitor update.");
                }

                JsonMemoryCache.RemoveByPrefix("servarr:sonarr:");
                return MessageResult(200, "Unmonitored in Sonarr. Existing files were left on disk.");
            }

            async Task<(int StatusCode, string Body, string ContentType)?> TryUnmonitorMovieAsync()
            {
                if (!IsRadarrConfigured(config))
                {
                    return null;
                }

                using HttpClient radarr = CreateClient(config.RadarrUrl!, config.RadarrApiKey!);
                JObject? movie = await FindByTmdbAsync(radarr, "movie", tmdbId, cancellationToken).ConfigureAwait(false);
                if (movie == null)
                {
                    return null;
                }

                movie["monitored"] = false;
                int? movieId = movie.Value<int?>("id");
                if (!movieId.HasValue)
                {
                    return MessageResult(500, "Radarr did not return a movie id.");
                }

                if (!await PutJsonAsync(radarr, $"movie/{movieId.Value}", movie, cancellationToken).ConfigureAwait(false))
                {
                    return MessageResult(502, "Radarr rejected the unmonitor update.");
                }

                JsonMemoryCache.RemoveByPrefix("servarr:radarr:");
                return MessageResult(200, "Unmonitored in Radarr. Existing files were left on disk.");
            }

            (int StatusCode, string Body, string ContentType)? preferred = isTv
                ? await TryUnmonitorSeriesAsync().ConfigureAwait(false)
                : await TryUnmonitorMovieAsync().ConfigureAwait(false);
            if (preferred != null)
            {
                return preferred.Value;
            }

            (int StatusCode, string Body, string ContentType)? fallback = isTv
                ? await TryUnmonitorMovieAsync().ConfigureAwait(false)
                : await TryUnmonitorSeriesAsync().ConfigureAwait(false);
            if (fallback != null)
            {
                return fallback.Value;
            }

            if (isTv)
            {
                return IsSonarrConfigured(config)
                    ? MessageResult(404, "This show is not in Sonarr.")
                    : MessageResult(400, "Sonarr is not configured in JellySeerr.");
            }

            return IsRadarrConfigured(config)
                ? MessageResult(404, "This movie is not in Radarr.")
                : MessageResult(400, "Radarr is not configured in JellySeerr.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JS • failed to unmonitor {MediaType}/{TmdbId}", mediaType, tmdbId);
            return MessageResult(502, "Could not reach Radarr/Sonarr to unmonitor this title.");
        }
    }

    public async Task EnrichRequestsAsync(JArray requests, CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        if (!IsRadarrConfigured(config) && !IsSonarrConfigured(config))
        {
            return;
        }

        List<ServarrRequestContext> contexts = requests
            .OfType<JObject>()
            .Select(BuildContext)
            .Where(c => c != null)
            .Cast<ServarrRequestContext>()
            .ToList();

        if (contexts.Count == 0)
        {
            return;
        }

        RadarrSnapshot? radarrSnapshot = IsRadarrConfigured(config)
            ? await LoadRadarrSnapshotAsync(config, contexts, cancellationToken).ConfigureAwait(false)
            : null;

        SonarrSnapshot? sonarrSnapshot = IsSonarrConfigured(config)
            ? await LoadSonarrSnapshotAsync(config, contexts, cancellationToken).ConfigureAwait(false)
            : null;

        foreach (ServarrRequestContext context in contexts)
        {
            ServarrProgressInfo? progress = string.Equals(context.Type, "tv", StringComparison.OrdinalIgnoreCase)
                ? BuildSeriesProgress(context, sonarrSnapshot)
                : BuildMovieProgress(context, radarrSnapshot);

            if (progress != null)
            {
                context.Request["servarrProgress"] = new JObject
                {
                    ["statusLabel"] = progress.StatusLabel,
                    ["statusKey"] = progress.StatusKey,
                    ["percent"] = progress.Percent,
                    ["downloadedBytes"] = progress.DownloadedBytes,
                    ["totalBytes"] = progress.TotalBytes,
                    ["isActive"] = progress.IsActive,
                    ["openUrl"] = progress.OpenUrl
                };
            }
        }
    }

    private static bool IsRadarrConfigured(PluginConfiguration config) => !string.IsNullOrWhiteSpace(config.RadarrUrl) && !string.IsNullOrWhiteSpace(config.RadarrApiKey);

    private static bool IsSonarrConfigured(PluginConfiguration config) => !string.IsNullOrWhiteSpace(config.SonarrUrl) && !string.IsNullOrWhiteSpace(config.SonarrApiKey);

    private static ServarrRequestContext? BuildContext(JObject request)
    {
        string? type = request.Value<string>("type");
        int? tmdbId = request.Value<int?>("tmdbId");
        if (!tmdbId.HasValue || string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        HashSet<int> seasonNumbers = request.Value<JArray>("seasonNumbers")?
            .Select(v => v.Type == JTokenType.Integer ? v.Value<int>() : (int?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToHashSet() ?? new HashSet<int>();

        return new ServarrRequestContext(request, type, tmdbId.Value, request.Value<int?>("externalServiceId"), seasonNumbers);
    }

    private async Task<RadarrSnapshot?> LoadRadarrSnapshotAsync(PluginConfiguration config, IReadOnlyCollection<ServarrRequestContext> contexts, CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient client = CreateClient(config.RadarrUrl!, config.RadarrApiKey!);
            List<JObject> queueRecords = await FetchAllQueueRecordsCachedAsync(client, includeMovie: true, cancellationToken)
                .ConfigureAwait(false);

            HashSet<int> tmdbIds = contexts
                .Where(c => !string.Equals(c.Type, "tv", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.TmdbId)
                .ToHashSet();

            Dictionary<int, JObject> moviesByTmdbId = new();
            if (tmdbIds.Count > 0)
            {
                JArray? movies = await GetCachedJsonArrayAsync(client, "servarr:radarr:movies", "movie", LibraryCacheTtl(), cancellationToken)
                    .ConfigureAwait(false);
                if (movies != null)
                {
                    foreach (JObject movie in movies.OfType<JObject>())
                    {
                        int? tmdbId = movie.Value<int?>("tmdbId");
                        if (tmdbId.HasValue && tmdbIds.Contains(tmdbId.Value))
                        {
                            moviesByTmdbId[tmdbId.Value] = movie;
                        }
                    }
                }

                foreach (ServarrRequestContext context in contexts.Where(c => !string.Equals(c.Type, "tv", StringComparison.OrdinalIgnoreCase) && !moviesByTmdbId.ContainsKey(c.TmdbId)))
                {
                    if (context.ExternalServiceId.HasValue)
                    {
                        JObject? movie = await GetJsonObjectAsync(client, $"movie/{context.ExternalServiceId.Value}", cancellationToken)
                            .ConfigureAwait(false);
                        if (movie != null)
                        {
                            moviesByTmdbId[context.TmdbId] = movie;
                        }
                    }
                }
            }

            Dictionary<int, List<JObject>> queueByMovieId = new();
            Dictionary<int, List<JObject>> queueByTmdbId = new();
            foreach (JObject record in queueRecords)
            {
                int? movieId = record.Value<int?>("movieId");
                if (movieId.HasValue)
                {
                    AddToLookup(queueByMovieId, movieId.Value, record);
                }

                int? tmdbId = record.Value<JObject>("movie")?.Value<int?>("tmdbId");
                if (tmdbId.HasValue)
                {
                    AddToLookup(queueByTmdbId, tmdbId.Value, record);
                }
            }

            return new RadarrSnapshot(NormalizeServarrBaseUrl(config.RadarrUrl!), moviesByTmdbId, queueByMovieId, queueByTmdbId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JS • failed to load Radarr progress snapshot from {RadarrUrl}", config.RadarrUrl);
            return null;
        }
    }

    private async Task<SonarrSnapshot?> LoadSonarrSnapshotAsync(PluginConfiguration config, IReadOnlyCollection<ServarrRequestContext> contexts, CancellationToken cancellationToken)
    {
        try
        {
            using HttpClient client = CreateClient(config.SonarrUrl!, config.SonarrApiKey!);
            List<JObject> queueRecords = await FetchAllQueueRecordsCachedAsync(client, includeMovie: false, cancellationToken)
                .ConfigureAwait(false);

            HashSet<int> tmdbIds = contexts
                .Where(c => string.Equals(c.Type, "tv", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.TmdbId)
                .ToHashSet();

            Dictionary<int, JObject> seriesByTmdbId = new();
            Dictionary<int, List<JObject>> episodesBySeriesId = new();
            if (tmdbIds.Count > 0)
            {
                JArray? seriesList = await GetCachedJsonArrayAsync(client, "servarr:sonarr:series", "series", LibraryCacheTtl(), cancellationToken)
                    .ConfigureAwait(false);
                if (seriesList != null)
                {
                    foreach (JObject series in seriesList.OfType<JObject>())
                    {
                        int? tmdbId = series.Value<int?>("tmdbId");
                        if (!tmdbId.HasValue || !tmdbIds.Contains(tmdbId.Value))
                        {
                            continue;
                        }

                        seriesByTmdbId[tmdbId.Value] = series;
                        int? seriesId = series.Value<int?>("id");
                        if (!seriesId.HasValue)
                        {
                            continue;
                        }

                        JArray? episodes = await GetCachedJsonArrayAsync(
                                client,
                                $"servarr:sonarr:episodes:{seriesId.Value}",
                                $"episode?seriesId={seriesId.Value}&includeEpisodeFile=true",
                                LibraryCacheTtl(),
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (episodes != null)
                        {
                            episodesBySeriesId[seriesId.Value] = episodes.OfType<JObject>().ToList();
                        }
                    }
                }

                foreach (ServarrRequestContext context in contexts.Where(c =>
                             string.Equals(c.Type, "tv", StringComparison.OrdinalIgnoreCase)
                             && !seriesByTmdbId.ContainsKey(c.TmdbId)))
                {
                    if (context.ExternalServiceId.HasValue)
                    {
                        JObject? series = await GetJsonObjectAsync(client, $"series/{context.ExternalServiceId.Value}", cancellationToken)
                            .ConfigureAwait(false);
                        if (series == null)
                        {
                            continue;
                        }

                        seriesByTmdbId[context.TmdbId] = series;
                        int? seriesId = series.Value<int?>("id");
                        if (!seriesId.HasValue)
                        {
                            continue;
                        }

                        JArray? episodes = await GetCachedJsonArrayAsync(
                                client,
                                $"servarr:sonarr:episodes:{seriesId.Value}",
                                $"episode?seriesId={seriesId.Value}&includeEpisodeFile=true",
                                LibraryCacheTtl(),
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (episodes != null)
                        {
                            episodesBySeriesId[seriesId.Value] = episodes.OfType<JObject>().ToList();
                        }
                    }
                }
            }

            Dictionary<int, List<JObject>> queueBySeriesId = new();
            foreach (JObject record in queueRecords)
            {
                int? seriesId = record.Value<int?>("seriesId");
                if (seriesId.HasValue)
                {
                    AddToLookup(queueBySeriesId, seriesId.Value, record);
                }
            }

            return new SonarrSnapshot(NormalizeServarrBaseUrl(config.SonarrUrl!), seriesByTmdbId, episodesBySeriesId, queueBySeriesId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JS • failed to load Sonarr progress snapshot from {SonarrUrl}", config.SonarrUrl);
            return null;
        }
    }

    private static ServarrProgressInfo? BuildMovieProgress(ServarrRequestContext context, RadarrSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            return null;
        }

        JObject? movie = snapshot.MoviesByTmdbId.GetValueOrDefault(context.TmdbId);
        int? movieId = movie?.Value<int?>("id") ?? context.ExternalServiceId;

        List<JObject> queueItems = movieId.HasValue && snapshot.QueueByMovieId.TryGetValue(movieId.Value, out List<JObject>? byId)
            ? byId
            : snapshot.QueueByTmdbId.GetValueOrDefault(context.TmdbId) ?? new List<JObject>();

        if (queueItems.Count > 0)
        {
            return BuildQueueProgress(queueItems, snapshot.BaseUrl, movie, isMovie: true);
        }

        if (movie == null)
        {
            return null;
        }

        return BuildLibraryProgress(
            hasFile: movie.Value<bool?>("hasFile") ?? false,
            monitored: movie.Value<bool?>("monitored") ?? false,
            isUnreleased: IsUnreleasedMedia(movie.Value<string>("status")),
            sizeOnDisk: ReadMovieSizeBytes(movie),
            openUrl: BuildServarrOpenUrl(snapshot.BaseUrl, GetTitleSlug(movie), isMovie: true));
    }

    private static ServarrProgressInfo? BuildSeriesProgress(ServarrRequestContext context, SonarrSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            return null;
        }

        if (!snapshot.SeriesByTmdbId.TryGetValue(context.TmdbId, out JObject? series))
        {
            return null;
        }

        int? seriesId = series.Value<int?>("id") ?? context.ExternalServiceId;
        string? seriesOpenUrl = BuildServarrOpenUrl(snapshot.BaseUrl, GetTitleSlug(series), isMovie: false);
        List<JObject> queueItems = seriesId.HasValue && snapshot.QueueBySeriesId.TryGetValue(seriesId.Value, out List<JObject>? queued)
            ? FilterQueueBySeasons(queued, context.SeasonNumbers)
            : new List<JObject>();

        if (queueItems.Count > 0)
        {
            return BuildQueueProgress(queueItems, snapshot.BaseUrl, series, isMovie: false);
        }

        ServarrProgressInfo? fromSeasons = BuildFromSeriesSeasons(series, context.SeasonNumbers, seriesOpenUrl);
        if (fromSeasons != null && HasLibraryFiles(fromSeasons))
        {
            return fromSeasons;
        }

        List<JObject> episodes = seriesId.HasValue && snapshot.EpisodesBySeriesId.TryGetValue(seriesId.Value, out List<JObject>? eps)
            ? FilterEpisodesBySeasons(eps, context.SeasonNumbers)
            : new List<JObject>();

        if (episodes.Count == 0)
        {
            if (fromSeasons != null)
            {
                return fromSeasons;
            }

            bool monitored = series.Value<bool?>("monitored") ?? false;
            JObject? stats = series.Value<JObject>("statistics");
            long sizeOnDisk = stats?.Value<long?>("sizeOnDisk") ?? series.Value<long?>("sizeOnDisk") ?? 0;
            bool hasFile = sizeOnDisk > 0;
            return BuildLibraryProgress(hasFile, monitored, isUnreleased: false, sizeOnDisk, seriesOpenUrl);
        }

        List<JObject> countable = episodes.Where(CountsTowardLibraryProgress).ToList();
        long totalSize = episodes.Where(e => e.Value<bool?>("hasFile") == true).Sum(ReadEpisodeSizeBytes);
        bool anyFile = episodes.Any(e => e.Value<bool?>("hasFile") == true);
        bool anyMonitored = episodes.Any(e => e.Value<bool?>("monitored") == true)
            || (series.Value<bool?>("monitored") ?? false);

        if (countable.Count == 0)
        {
            if (anyFile)
            {
                return BuildLibraryProgress(true, anyMonitored, false, totalSize, seriesOpenUrl);
            }

            if (fromSeasons != null)
            {
                return fromSeasons;
            }

            bool allUnreleased = episodes.All(e =>
                IsUnreleasedMedia(e.Value<string>("airDateUtc") ?? e.Value<string>("airDate")));
            return BuildLibraryProgress(false, anyMonitored, allUnreleased, 0, seriesOpenUrl);
        }

        int fileCount = countable.Count(e => e.Value<bool?>("hasFile") == true);
        bool allHaveFiles = fileCount == countable.Count;
        if (anyFile && !allHaveFiles)
        {
            return BuildPartialProgress(fileCount, countable.Count, totalSize, seriesOpenUrl);
        }

        if (allHaveFiles)
        {
            return BuildLibraryProgress(true, anyMonitored, false, totalSize, seriesOpenUrl);
        }

        return fromSeasons ?? BuildLibraryProgress(false, anyMonitored, false, 0, seriesOpenUrl);
    }

    private static bool HasLibraryFiles(ServarrProgressInfo progress) =>
        progress.DownloadedBytes > 0
        || string.Equals(progress.StatusKey, "partial", StringComparison.OrdinalIgnoreCase)
        || progress.StatusKey.StartsWith("downloaded-", StringComparison.OrdinalIgnoreCase);

    private static ServarrProgressInfo? BuildFromSeriesSeasons(JObject series, HashSet<int> seasonNumbers, string? openUrl)
    {
        JArray? seasons = series.Value<JArray>("seasons");
        if (seasons == null || seasons.Count == 0)
        {
            return null;
        }

        bool includeSpecials = AdvancedSettingsHelper.Resolve(JellySeerrPlugin.Instance.Configuration).Servarr.IncludeSpecialsInSeriesProgress;
        List<JObject> wanted = seasons.OfType<JObject>().Where(season =>
        {
            int number = season.Value<int?>("seasonNumber") ?? -1;
            if (number < 0)
            {
                return false;
            }

            if (number == 0 && !includeSpecials)
            {
                return false;
            }

            return seasonNumbers.Count == 0 || seasonNumbers.Contains(number);
        }).ToList();

        if (wanted.Count == 0)
        {
            return null;
        }

        if (seasonNumbers.Count == 0)
        {
            List<JObject> present = wanted.Where(season =>
            {
                JObject? stats = season.Value<JObject>("statistics");
                int files = stats?.Value<int?>("episodeFileCount") ?? 0;
                bool monitored = season.Value<bool?>("monitored") ?? false;
                return files > 0 || monitored;
            }).ToList();
            if (present.Count > 0)
            {
                wanted = present;
            }
        }

        int fileCount = 0;
        int episodeCount = 0;
        long size = 0;
        bool anyMonitored = false;
        bool anyStats = false;
        foreach (JObject season in wanted)
        {
            anyMonitored |= season.Value<bool?>("monitored") == true;
            JObject? stats = season.Value<JObject>("statistics");
            if (stats == null)
            {
                continue;
            }

            anyStats = true;
            fileCount += stats.Value<int?>("episodeFileCount") ?? 0;
            episodeCount += stats.Value<int?>("episodeCount") ?? 0;
            size += stats.Value<long?>("sizeOnDisk") ?? 0;
        }

        if (!anyStats)
        {
            return null;
        }

        if (fileCount > 0 && (episodeCount <= 0 || fileCount >= episodeCount))
        {
            return BuildLibraryProgress(true, anyMonitored, false, size, openUrl);
        }

        if (fileCount > 0)
        {
            return BuildPartialProgress(fileCount, episodeCount, size, openUrl);
        }

        return BuildLibraryProgress(false, anyMonitored, false, 0, openUrl);
    }

    private static ServarrProgressInfo BuildPartialProgress(int fileCount, int episodeCount, long sizeOnDisk, string? openUrl)
    {
        int percent = episodeCount > 0 ? (int)Math.Round(fileCount * 100.0 / episodeCount) : 0;
        return new ServarrProgressInfo
        {
            StatusLabel = $"{fileCount} of {episodeCount} episodes",
            StatusKey = "partial",
            Percent = Math.Clamp(percent, 0, 100),
            DownloadedBytes = sizeOnDisk,
            TotalBytes = sizeOnDisk,
            IsActive = false,
            OpenUrl = openUrl
        };
    }

    private static ServarrProgressInfo BuildQueueProgress(IReadOnlyCollection<JObject> queueItems, string baseUrl, JObject? media, bool isMovie)
    {
        long totalSize = queueItems.Sum(ReadQueueItemSizeBytes);
        long sizeLeft = queueItems.Sum(ReadQueueItemSizeLeftBytes);
        long downloaded = Math.Max(0, totalSize - sizeLeft);
        int percent = totalSize > 0 ? (int)Math.Round(downloaded / (double)totalSize * 100) : 0;
        string? titleSlug = GetTitleSlug(media)
            ?? queueItems
                .Select(item => GetTitleSlug(isMovie ? item["movie"] as JObject : item["series"] as JObject))
                .FirstOrDefault(slug => !string.IsNullOrWhiteSpace(slug));

        return new ServarrProgressInfo
        {
            StatusLabel = "Queued",
            StatusKey = "queued",
            Percent = percent,
            DownloadedBytes = downloaded,
            TotalBytes = totalSize,
            IsActive = true,
            OpenUrl = BuildServarrOpenUrl(baseUrl, titleSlug, isMovie)
        };
    }

    private static ServarrProgressInfo BuildLibraryProgress(bool hasFile, bool monitored, bool isUnreleased, long sizeOnDisk, string? openUrl = null)
    {
        if (isUnreleased)
        {
            return new ServarrProgressInfo
            {
                StatusLabel = "Unreleased",
                StatusKey = "unreleased",
                Percent = 0,
                DownloadedBytes = 0,
                TotalBytes = 0,
                IsActive = false,
                OpenUrl = openUrl
            };
        }

        bool downloadedActive = AdvancedSettingsHelper.Resolve(JellySeerrPlugin.Instance.Configuration).Servarr.DownloadedProgressIsActive;

        if (hasFile && monitored)
        {
            return new ServarrProgressInfo
            {
                StatusLabel = "Downloaded (Monitored)",
                StatusKey = "downloaded-monitored",
                Percent = 100,
                DownloadedBytes = sizeOnDisk,
                TotalBytes = sizeOnDisk,
                IsActive = downloadedActive,
                OpenUrl = openUrl
            };
        }

        if (hasFile)
        {
            return new ServarrProgressInfo
            {
                StatusLabel = "Downloaded (Unmonitored)",
                StatusKey = "downloaded-unmonitored",
                Percent = 100,
                DownloadedBytes = sizeOnDisk,
                TotalBytes = sizeOnDisk,
                IsActive = downloadedActive,
                OpenUrl = openUrl
            };
        }

        if (monitored)
        {
            return new ServarrProgressInfo
            {
                StatusLabel = "Missing (Monitored)",
                StatusKey = "missing-monitored",
                Percent = 0,
                DownloadedBytes = 0,
                TotalBytes = 0,
                IsActive = false,
                OpenUrl = openUrl
            };
        }

        return new ServarrProgressInfo
        {
            StatusLabel = "Missing (Unmonitored)",
            StatusKey = "missing-unmonitored",
            Percent = 0,
            DownloadedBytes = 0,
            TotalBytes = 0,
            IsActive = false,
            OpenUrl = openUrl
        };
    }

    private static string NormalizeServarrBaseUrl(string baseUrl) => baseUrl.Trim().TrimEnd('/');

    private static string? GetTitleSlug(JObject? media) => media?.Value<string>("titleSlug");

    private static string? BuildServarrOpenUrl(string baseUrl, string? titleSlug, bool isMovie) =>
        !string.IsNullOrWhiteSpace(titleSlug)
            ? $"{baseUrl}/{(isMovie ? "movie" : "series")}/{titleSlug.Trim()}"
            : null;

    private static bool IsUnreleasedMedia(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (string.Equals(value, "announced", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "inCinemas", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsed)
            && parsed.ToUniversalTime() > DateTime.UtcNow;
    }

    private static bool CountsTowardLibraryProgress(JObject episode)
    {
        if (episode.Value<bool?>("hasFile") == true)
        {
            return true;
        }

        string? air = episode.Value<string>("airDateUtc") ?? episode.Value<string>("airDate");
        if (string.IsNullOrWhiteSpace(air))
        {
            return false;
        }

        return !IsUnreleasedMedia(air);
    }

    private static (int StatusCode, string Body, string ContentType) MessageResult(int statusCode, string message) =>
        (statusCode, new JObject { ["message"] = message }.ToString(Newtonsoft.Json.Formatting.None), "application/json");

    private static async Task<JObject?> FindByTmdbAsync(HttpClient client, string resource, int tmdbId, CancellationToken cancellationToken)
    {
        JArray? list = await GetJsonArrayAsync(client, $"{resource}?tmdbId={tmdbId}", cancellationToken).ConfigureAwait(false);
        JObject? match = list?.OfType<JObject>().FirstOrDefault(item => item.Value<int?>("tmdbId") == tmdbId);
        if (match != null)
        {
            return match;
        }

        JArray? all = await GetJsonArrayAsync(client, resource, cancellationToken).ConfigureAwait(false);
        return all?.OfType<JObject>().FirstOrDefault(item => item.Value<int?>("tmdbId") == tmdbId);
    }

    private static async Task<bool> PutJsonAsync(HttpClient client, string path, JObject body, CancellationToken cancellationToken)
    {
        using StringContent content = new(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await client.PutAsync(path.TrimStart('/'), content, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private static List<JObject> FilterQueueBySeasons(IEnumerable<JObject> queueItems, HashSet<int> seasonNumbers)
    {
        if (seasonNumbers.Count == 0)
        {
            return queueItems.ToList();
        }

        return queueItems
            .Where(item =>
            {
                int? seasonNumber = item.Value<JObject>("episode")?.Value<int?>("seasonNumber");
                return seasonNumber.HasValue && seasonNumbers.Contains(seasonNumber.Value);
            })
            .ToList();
    }

    private static List<JObject> FilterEpisodesBySeasons(IEnumerable<JObject> episodes, HashSet<int> seasonNumbers)
    {
        bool includeSpecials = AdvancedSettingsHelper.Resolve(JellySeerrPlugin.Instance.Configuration).Servarr.IncludeSpecialsInSeriesProgress;
        IEnumerable<JObject> scoped = includeSpecials
            ? episodes
            : episodes.Where(e => e.Value<int?>("seasonNumber") != 0);
        if (seasonNumbers.Count == 0)
        {
            return scoped.ToList();
        }

        return scoped.Where(e =>
        {
            int? seasonNumber = e.Value<int?>("seasonNumber");
            return seasonNumber.HasValue && seasonNumbers.Contains(seasonNumber.Value);
        }).ToList();
    }

    private static async Task<List<JObject>> FetchAllQueueRecordsAsync(HttpClient client, bool includeMovie, CancellationToken cancellationToken)
    {
        List<JObject> records = new();
        int page = 1;
        const int pageSize = 250;

        while (true)
        {
            string path = includeMovie
                ? $"queue?page={page}&pageSize={pageSize}&includeMovie=true"
                : $"queue?page={page}&pageSize={pageSize}&includeSeries=true&includeEpisode=true";

            JObject? payload = await GetJsonObjectAsync(client, path, cancellationToken).ConfigureAwait(false);
            if (payload == null)
            {
                break;
            }

            JArray? pageRecords = payload.Value<JArray>("records");
            if (pageRecords == null || pageRecords.Count == 0)
            {
                break;
            }

            records.AddRange(pageRecords.OfType<JObject>());

            int totalRecords = payload.Value<int?>("totalRecords") ?? records.Count;
            if (records.Count >= totalRecords)
            {
                break;
            }

            page++;
        }

        return records;
    }

    private static async Task<JArray?> GetJsonArrayAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync(path.TrimStart('/'), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        JToken token = JToken.Parse(raw);
        return token as JArray;
    }

    private static async Task<JObject?> GetJsonObjectAsync(HttpClient client, string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync(path.TrimStart('/'), cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JObject.Parse(raw);
    }

    private static HttpClient CreateClient(string baseUrl, string apiKey)
    {
        string normalized = baseUrl.Trim().TrimEnd('/');
        HttpClient client = new() { BaseAddress = new Uri(normalized + "/api/v3/") };
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private static TimeSpan LibraryCacheTtl()
    {
        int seconds = Math.Clamp(JellySeerrPlugin.Instance.Configuration.ServarrCacheSeconds, 0, 3600);
        return TimeSpan.FromSeconds(seconds);
    }

    private static TimeSpan QueueCacheTtl()
    {
        if (JellySeerrPlugin.Instance.Configuration.ServarrCacheSeconds <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(15);
    }

    private static long ReadMovieSizeBytes(JObject movie)
    {
        long sizeOnDisk = movie.Value<long?>("sizeOnDisk") ?? 0;
        if (sizeOnDisk > 0)
        {
            return sizeOnDisk;
        }

        return movie.Value<JObject>("movieFile")?.Value<long?>("size") ?? 0;
    }

    private static long ReadEpisodeSizeBytes(JObject episode)
    {
        long sizeOnDisk = episode.Value<long?>("sizeOnDisk") ?? 0;
        if (sizeOnDisk > 0)
        {
            return sizeOnDisk;
        }

        return episode.Value<JObject>("episodeFile")?.Value<long?>("size") ?? 0;
    }

    private static long ReadQueueItemSizeBytes(JObject item)
    {
        double size = item.Value<double?>("size") ?? 0;
        return size > 0 ? (long)Math.Round(size) : 0;
    }

    private static long ReadQueueItemSizeLeftBytes(JObject item)
    {
        double sizeLeft = item.Value<double?>("sizeleft") ?? 0;
        return sizeLeft > 0 ? (long)Math.Round(sizeLeft) : 0;
    }

    private async Task<JArray?> GetCachedJsonArrayAsync(
        HttpClient client,
        string cacheKey,
        string path,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        if (ttl > TimeSpan.Zero && JsonMemoryCache.TryGet(cacheKey, out JToken? cached) && cached is JArray array)
        {
            return array;
        }

        JArray? fresh = await GetJsonArrayAsync(client, path, cancellationToken).ConfigureAwait(false);
        if (fresh != null && ttl > TimeSpan.Zero)
        {
            JsonMemoryCache.Set(cacheKey, fresh, ttl);
        }

        return fresh;
    }

    private async Task<List<JObject>> FetchAllQueueRecordsCachedAsync(HttpClient client, bool includeMovie, CancellationToken cancellationToken)
    {
        string cacheKey = includeMovie ? "servarr:radarr:queue" : "servarr:sonarr:queue";
        TimeSpan ttl = QueueCacheTtl();
        if (ttl > TimeSpan.Zero && JsonMemoryCache.TryGet(cacheKey, out JToken? cached) && cached is JArray array)
        {
            return array.OfType<JObject>().ToList();
        }

        List<JObject> records = await FetchAllQueueRecordsAsync(client, includeMovie, cancellationToken).ConfigureAwait(false);
        if (ttl > TimeSpan.Zero)
        {
            JArray payload = new();
            foreach (JObject record in records)
            {
                payload.Add(record);
            }

            JsonMemoryCache.Set(cacheKey, payload, ttl);
        }

        return records;
    }

    private static void AddToLookup(Dictionary<int, List<JObject>> lookup, int key, JObject value)
    {
        if (!lookup.TryGetValue(key, out List<JObject>? list))
        {
            list = new List<JObject>();
            lookup[key] = list;
        }

        list.Add(value);
    }

    private sealed record ServarrRequestContext(
        JObject Request,
        string Type,
        int TmdbId,
        int? ExternalServiceId,
        HashSet<int> SeasonNumbers);

    private sealed record RadarrSnapshot(
        string BaseUrl,
        Dictionary<int, JObject> MoviesByTmdbId,
        Dictionary<int, List<JObject>> QueueByMovieId,
        Dictionary<int, List<JObject>> QueueByTmdbId);

    private sealed record SonarrSnapshot(
        string BaseUrl,
        Dictionary<int, JObject> SeriesByTmdbId,
        Dictionary<int, List<JObject>> EpisodesBySeriesId,
        Dictionary<int, List<JObject>> QueueBySeriesId);

    private sealed class ServarrProgressInfo
    {
        public string StatusLabel { get; set; } = string.Empty;

        public string StatusKey { get; set; } = string.Empty;

        public int Percent { get; set; }

        public long DownloadedBytes { get; set; }

        public long TotalBytes { get; set; }

        public bool IsActive { get; set; }

        public string? OpenUrl { get; set; }
    }
}