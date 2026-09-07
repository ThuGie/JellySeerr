namespace Jellyfin.Plugin.JellySeerr.Model;

public sealed class CachedImageFile
{
    public string FilePath { get; init; } = string.Empty;

    public string ContentType { get; init; } = "image/jpeg";

    public string ETag { get; init; } = string.Empty;

    public DateTime LastModified { get; init; }

    public int MaxAgeSeconds { get; init; }
}