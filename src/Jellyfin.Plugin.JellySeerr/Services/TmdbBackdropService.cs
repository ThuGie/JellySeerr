using System.Collections.Concurrent;
using Jellyfin.Plugin.JellySeerr.Configuration;
using Jellyfin.Plugin.JellySeerr.Configuration.Advanced;
using Jellyfin.Plugin.JellySeerr.Helpers;
using Jellyfin.Plugin.JellySeerr.Model;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellySeerr.Services;

public class TmdbBackdropService
{
    private readonly HttpClient _httpClient;
    private readonly ImageCacheService _imageCacheService;
    private readonly ILogger<TmdbBackdropService> _logger;
    private readonly ConcurrentDictionary<string, CachedBackdropDto> _cache = new();

    public TmdbBackdropService(HttpClient httpClient, ImageCacheService imageCacheService, ILogger<TmdbBackdropService> logger)
    {
        _httpClient = httpClient;
        _imageCacheService = imageCacheService;
        _logger = logger;
    }

    public async Task<CachedBackdropDto?> GetCachedBackdropAsync(string mediaType, int tmdbId, bool preferNeutral = false, CancellationToken cancellationToken = default)
    {
        BackdropBatchItemDto? match = await ResolveBackdropItemAsync(
            new BackdropBatchRequestItemDto
            {
                MediaType = mediaType,
                TmdbId = tmdbId
            },
            preferNeutral,
            cancellationToken).ConfigureAwait(false);

        if (match == null || string.IsNullOrEmpty(match.BackdropUrl))
        {
            return null;
        }

        return new CachedBackdropDto
        {
            BackdropUrl = match.BackdropUrl,
            TmdbBackdropPath = match.TmdbBackdropPath,
            HasEnglishBackdrop = match.HasEnglishBackdrop
        };
    }

    public async Task<List<BackdropBatchItemDto>> GetCachedBackdropsAsync(IEnumerable<BackdropBatchRequestItemDto> items, CancellationToken cancellationToken = default)
    {
        List<BackdropBatchRequestItemDto> uniqueItems = items
            .Where(item => item.TmdbId > 0 && (string.Equals(item.MediaType, "movie", StringComparison.OrdinalIgnoreCase) || string.Equals(item.MediaType, "tv", StringComparison.OrdinalIgnoreCase)))
            .GroupBy(item => $"{item.MediaType.ToLowerInvariant()}:{item.TmdbId}")
            .Select(group => group.First())
            .ToList();

        if (uniqueItems.Count == 0)
        {
            return new List<BackdropBatchItemDto>();
        }

        int concurrency = AdvancedSettingsHelper.Resolve(JellySeerrPlugin.Instance.Configuration).Tmdb.BackdropBatchConcurrency;
        using SemaphoreSlim semaphore = new(concurrency, concurrency);
        IEnumerable<Task<BackdropBatchItemDto?>> tasks = uniqueItems.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await ResolveBackdropItemAsync(item, preferNeutral: false, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });

        BackdropBatchItemDto?[] resolved = await Task.WhenAll(tasks).ConfigureAwait(false);
        return resolved
            .Where(item => item != null && !string.IsNullOrEmpty(item.BackdropUrl))
            .Cast<BackdropBatchItemDto>()
            .ToList();
    }

    private async Task<BackdropBatchItemDto?> ResolveBackdropItemAsync(BackdropBatchRequestItemDto item, bool preferNeutral, CancellationToken cancellationToken)
    {
        string mediaType = item.MediaType.ToLowerInvariant();
        int tmdbId = item.TmdbId;

        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        string? apiKey = config.TmdbApiKey?.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            return null;
        }

        AdvancedTmdbSettings tmdbSettings = AdvancedSettingsHelper.Resolve(config).Tmdb;
        string cacheKey = BuildBackdropCacheKey(mediaType, tmdbId, tmdbSettings, preferNeutral);

        CachedBackdropDto? cached = TryGetCachedBackdrop(cacheKey, tmdbSettings);
        if (cached != null)
        {
            return ToBatchItem(mediaType, tmdbId, cached);
        }

        BackdropPickResult pick = await TmdbBackdropHelper.FetchBackdropAsync(
            _httpClient,
            mediaType,
            tmdbId,
            apiKey,
            tmdbSettings.BackdropLanguageFilter,
            preferOriginalLanguage: !preferNeutral && tmdbSettings.PreferOriginalLanguageImages,
            preferNeutral: preferNeutral,
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(pick.FilePath))
        {
            return null;
        }

        string path = pick.FilePath.StartsWith('/') ? pick.FilePath : "/" + pick.FilePath;
        string backdropSize = string.IsNullOrWhiteSpace(tmdbSettings.BackdropImageSize) ? "w780" : tmdbSettings.BackdropImageSize;
        string sourceUrl = $"https://image.tmdb.org/t/p/{backdropSize}{path}";
        string backdropUrl = ImageCacheHelper.GetCachedImageUrl(_imageCacheService, sourceUrl, _logger);

        if (string.IsNullOrEmpty(backdropUrl))
        {
            return null;
        }

        var dto = new CachedBackdropDto
        {
            BackdropUrl = backdropUrl,
            TmdbBackdropPath = path,
            HasEnglishBackdrop = pick.HasEnglishBackdrop
        };
        _cache[cacheKey] = dto;
        return ToBatchItem(mediaType, tmdbId, dto);
    }

    private CachedBackdropDto? TryGetCachedBackdrop(string cacheKey, AdvancedTmdbSettings tmdbSettings)
    {
        if (!_cache.TryGetValue(cacheKey, out CachedBackdropDto? cached))
        {
            return null;
        }

        if (string.IsNullOrEmpty(cached.TmdbBackdropPath))
        {
            _cache.TryRemove(cacheKey, out _);
            return null;
        }

        string backdropSize = string.IsNullOrWhiteSpace(tmdbSettings.BackdropImageSize) ? "w780" : tmdbSettings.BackdropImageSize;
        string cachedSourceUrl = $"https://image.tmdb.org/t/p/{backdropSize}{cached.TmdbBackdropPath}";
        string refreshedUrl = ImageCacheHelper.GetCachedImageUrl(_imageCacheService, cachedSourceUrl, _logger);
        if (!string.IsNullOrEmpty(refreshedUrl))
        {
            return new CachedBackdropDto
            {
                BackdropUrl = refreshedUrl,
                TmdbBackdropPath = cached.TmdbBackdropPath,
                HasEnglishBackdrop = cached.HasEnglishBackdrop
            };
        }

        _cache.TryRemove(cacheKey, out _);
        return null;
    }

    private static string BuildBackdropCacheKey(string mediaType, int tmdbId, AdvancedTmdbSettings tmdbSettings, bool preferNeutral)
    {
        string languageFilter = string.IsNullOrWhiteSpace(tmdbSettings.BackdropLanguageFilter)
            ? "en,null,en-US"
            : tmdbSettings.BackdropLanguageFilter.Trim();
        string preferOriginal = !preferNeutral && tmdbSettings.PreferOriginalLanguageImages ? "original" : "default";
        string neutral = preferNeutral ? "neutral" : "card";

        return $"{mediaType}:{tmdbId}:lang={languageFilter.ToLowerInvariant()}:mode={preferOriginal}:pick={neutral}";
    }

    private static BackdropBatchItemDto ToBatchItem(string mediaType, int tmdbId, CachedBackdropDto cached) =>
        new()
        {
            MediaType = mediaType,
            TmdbId = tmdbId,
            BackdropUrl = cached.BackdropUrl,
            TmdbBackdropPath = cached.TmdbBackdropPath,
            HasEnglishBackdrop = cached.HasEnglishBackdrop
        };

    public sealed class CachedBackdropDto
    {
        public string BackdropUrl { get; init; } = string.Empty;

        public string TmdbBackdropPath { get; init; } = string.Empty;

        public bool HasEnglishBackdrop { get; init; }
    }
}