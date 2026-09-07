namespace Jellyfin.Plugin.JellySeerr.Model;

public sealed class BackdropBatchResponseDto
{
    public List<BackdropBatchItemDto> Items { get; set; } = new();
}

public sealed class BackdropBatchItemDto
{
    public string MediaType { get; set; } = string.Empty;

    public int TmdbId { get; set; }

    public string BackdropUrl { get; set; } = string.Empty;

    public string TmdbBackdropPath { get; set; } = string.Empty;

    public bool HasEnglishBackdrop { get; set; }
}