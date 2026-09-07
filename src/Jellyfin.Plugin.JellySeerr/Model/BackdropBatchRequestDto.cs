namespace Jellyfin.Plugin.JellySeerr.Model;

public sealed class BackdropBatchRequestDto
{
    public List<BackdropBatchRequestItemDto> Items { get; set; } = new();
}

public sealed class BackdropBatchRequestItemDto
{
    public string MediaType { get; set; } = string.Empty;

    public int TmdbId { get; set; }
}