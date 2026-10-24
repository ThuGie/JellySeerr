using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.JellySeerr.Configuration;
using Jellyfin.Plugin.JellySeerr.Helpers;
using Jellyfin.Plugin.JellySeerr.Model;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellySeerr.Services;

public class ImageCacheService : IDisposable
{
    private readonly ILogger<ImageCacheService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly ConcurrentDictionary<string, CachedImageDto> _imageCache = new();
    private readonly SingleFlight<string?> _inflightDownloads = new();
    private readonly Func<PluginConfiguration> _configuration;
    private readonly object _indexLock = new();
    private readonly Timer _indexFlush;
    private readonly SemaphoreSlim _downloadSlots = new(6);

    public ImageCacheService(
        ILogger<ImageCacheService> logger,
        IApplicationPaths applicationPaths,
        HttpClient httpClient,
        Func<PluginConfiguration>? configuration = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _configuration = configuration ?? (() => JellySeerrPlugin.Instance.Configuration);
        _cacheDirectory = Path.Combine(applicationPaths.CachePath, "JellySeerr", "Images");
        Directory.CreateDirectory(_cacheDirectory);
        LoadCacheIndex();
        _indexFlush = new Timer(_ => SaveCacheIndex(), null, Timeout.Infinite, Timeout.Infinite);
    }

    // Card mapping does no network I/O. Images download only when the browser requests them.
    public string RegisterImage(string sourceUrl)
    {
        string key = GenerateCacheKey(sourceUrl);
        if (_imageCache.Count >= Math.Max(1, _configuration().MaxImageCacheEntries) && !_imageCache.ContainsKey(key)) EvictOldEntries();
        _imageCache.TryAdd(key, new CachedImageDto { CacheKey = key, SourceUrl = sourceUrl, CachedAt = DateTime.UtcNow });
        return key;
    }

    public string? GetRegisteredSource(string key) => _imageCache.TryGetValue(key, out var entry) ? entry.SourceUrl : null;

    public async Task<string?> GetOrCacheImage(string sourceUrl, int cacheTimeoutSeconds)
    {
        if (string.IsNullOrEmpty(sourceUrl))
        {
            return null;
        }

        string cacheKey = GenerateCacheKey(sourceUrl);

        if (IsValidCacheKey(cacheKey))
        {
            return cacheKey;
        }

        return await GetOrStartDownload(sourceUrl, cacheKey, cacheTimeoutSeconds).ConfigureAwait(false);
    }

    public async Task<CachedImageFile?> GetCachedImageFileAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (!_imageCache.TryGetValue(cacheKey, out CachedImageDto? cachedInfo))
        {
            return null;
        }

        if (cachedInfo.ExpiresAt > DateTime.UtcNow && File.Exists(cachedInfo.FilePath))
        {
            return BuildCachedImageFile(cachedInfo);
        }

        if (!string.IsNullOrEmpty(cachedInfo.SourceUrl))
        {
            int cacheTimeout = _configuration().CacheTimeoutSeconds;
            string? refreshedKey = await GetOrCacheImage(cachedInfo.SourceUrl, cacheTimeout).WaitAsync(cancellationToken).ConfigureAwait(false);
            if (refreshedKey == cacheKey
                && _imageCache.TryGetValue(cacheKey, out cachedInfo)
                && File.Exists(cachedInfo.FilePath))
            {
                return BuildCachedImageFile(cachedInfo);
            }
        }

        if (cachedInfo != null && File.Exists(cachedInfo.FilePath)) return BuildCachedImageFile(cachedInfo);
        return null;
    }

    private async Task<string?> GetOrStartDownload(string sourceUrl, string cacheKey, int cacheTimeoutSeconds)
    {
        return await _inflightDownloads.RunAsync(cacheKey, async () =>
        {
            if (IsValidCacheKey(cacheKey)) return cacheKey;
            await _downloadSlots.WaitAsync().ConfigureAwait(false);
            try { return await DownloadAndCacheImage(sourceUrl, cacheKey, cacheTimeoutSeconds).ConfigureAwait(false); }
            finally { _downloadSlots.Release(); }
        }).ConfigureAwait(false);
    }

    private bool IsValidCacheKey(string cacheKey)
    {
        return _imageCache.TryGetValue(cacheKey, out CachedImageDto? cachedInfo)
               && cachedInfo.ExpiresAt > DateTime.UtcNow
               && File.Exists(cachedInfo.FilePath);
    }

    private async Task<string?> DownloadAndCacheImage(string sourceUrl, string cacheKey, int cacheTimeoutSeconds)
    {
        try
        {
            if (IsValidCacheKey(cacheKey))
            {
                return cacheKey;
            }

            PluginConfiguration config = _configuration();
            if (_imageCache.Count >= config.MaxImageCacheEntries && !_imageCache.ContainsKey(cacheKey))
            {
                EvictOldEntries();
            }

            using HttpResponseMessage response = await _httpClient.GetAsync(sourceUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            byte[] imageData = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            string contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            string extension = GetExtensionFromContentType(contentType);
            string filePath = Path.Combine(_cacheDirectory, $"{cacheKey}{extension}");
            await File.WriteAllBytesAsync(filePath + ".tmp", imageData).ConfigureAwait(false);
            File.Move(filePath + ".tmp", filePath, overwrite: true);

            DateTime cachedAt = DateTime.UtcNow;
            _imageCache[cacheKey] = new CachedImageDto
            {
                CacheKey = cacheKey,
                SourceUrl = sourceUrl,
                FilePath = filePath,
                ContentType = contentType,
                CachedAt = cachedAt,
                ExpiresAt = cachedAt.AddSeconds(cacheTimeoutSeconds)
            };
            _indexFlush.Change(250, Timeout.Infinite);
            return cacheKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JS • error caching image from {SourceUrl}", sourceUrl);
            return null;
        }
    }

    private CachedImageFile BuildCachedImageFile(CachedImageDto cachedInfo)
    {
        DateTime lastModified = cachedInfo.CachedAt;
        try
        {
            lastModified = File.GetLastWriteTimeUtc(cachedInfo.FilePath);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "JS • failed to read last write time for {FilePath}", cachedInfo.FilePath);
        }

        int maxAgeSeconds = Math.Max(0, (int)Math.Ceiling((cachedInfo.ExpiresAt - DateTime.UtcNow).TotalSeconds));
        long fileLength = 0;
        try
        {
            fileLength = new FileInfo(cachedInfo.FilePath).Length;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "JS • failed to read file length for {FilePath}", cachedInfo.FilePath);
        }

        return new CachedImageFile
        {
            FilePath = cachedInfo.FilePath,
            ContentType = cachedInfo.ContentType,
            LastModified = lastModified,
            MaxAgeSeconds = maxAgeSeconds,
            ETag = BuildETag(cachedInfo.CacheKey, lastModified, fileLength)
        };
    }

    private static string BuildETag(string cacheKey, DateTime lastModified, long fileLength)
    {
        return $"\"{cacheKey}-{lastModified.Ticks:x}-{fileLength:x}\"";
    }

    private void CleanupCacheEntry(string cacheKey)
    {
        if (_imageCache.TryRemove(cacheKey, out CachedImageDto? cachedInfo) && File.Exists(cachedInfo.FilePath))
        {
            try
            {
                File.Delete(cachedInfo.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JS • failed to delete cache file {FilePath}", cachedInfo.FilePath);
            }
        }
    }

    private void EvictOldEntries()
    {
        int maxEntries = _configuration().MaxImageCacheEntries;
        foreach (string key in _imageCache.Values
                     .OrderBy(x => x.CachedAt)
                     .Take(Math.Max(1, maxEntries / 10))
                     .Select(x => x.CacheKey))
        {
            CleanupCacheEntry(key);
        }

        _indexFlush.Change(250, Timeout.Infinite);
    }

    private void LoadCacheIndex()
    {
        string indexPath = Path.Combine(_cacheDirectory, "cache-index.json");
        if (!File.Exists(indexPath))
        {
            return;
        }

        try
        {
            CachedImageDto[]? entries = JsonSerializer.Deserialize<CachedImageDto[]>(File.ReadAllText(indexPath));
            if (entries == null)
            {
                return;
            }

            foreach (CachedImageDto entry in entries)
            {
                if (entry.ExpiresAt > DateTime.UtcNow && File.Exists(entry.FilePath))
                {
                    _imageCache[entry.CacheKey] = entry;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JS • error loading cache index");
        }
    }

    private void SaveCacheIndex()
    {
        try
        {
            lock (_indexLock)
            {
                string indexPath = Path.Combine(_cacheDirectory, "cache-index.json");
                string json = JsonSerializer.Serialize(_imageCache.Values.Where(e => !string.IsNullOrEmpty(e.FilePath)).ToArray());
                File.WriteAllText(indexPath + ".tmp", json);
                File.Move(indexPath + ".tmp", indexPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JS • error saving cache index");
        }
    }

    public void Dispose()
    {
        _indexFlush.Dispose();
        SaveCacheIndex();
    }

    private static string GenerateCacheKey(string sourceUrl)
    {
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(sourceUrl));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string GetExtensionFromContentType(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/gif" => ".gif",
        "image/webp" => ".webp",
        _ => ".jpg"
    };
}
