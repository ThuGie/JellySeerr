using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellySeerr.Model;

/// <summary>
/// Request body for create/update. Use one JSON name per field — dual Pascal/camel
/// properties collide under Jellyfin's case-insensitive System.Text.Json options.
/// Clients may still send MediaType/MediaId; case-insensitive binding maps them.
/// </summary>
public class RequestPayload
{
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = string.Empty;

    [JsonPropertyName("mediaId")]
    public int MediaId { get; set; }

    [JsonPropertyName("serverId")]
    public int? ServerId { get; set; }

    [JsonPropertyName("profileId")]
    public int? ProfileId { get; set; }

    [JsonPropertyName("rootFolder")]
    public string? RootFolder { get; set; }

    [JsonPropertyName("is4k")]
    public bool Is4k { get; set; }

    [JsonPropertyName("isAnime")]
    public bool IsAnime { get; set; }

    [JsonPropertyName("seasons")]
    public List<int>? Seasons { get; set; }

    [JsonPropertyName("tags")]
    public List<int>? Tags { get; set; }

    [JsonPropertyName("languageProfileId")]
    public int? LanguageProfileId { get; set; }
}

public class IssuePayload
{
    [JsonPropertyName("issueType")]
    public int IssueType { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("mediaId")]
    public int MediaId { get; set; }
}

public class BulkCancelPayload
{
    [JsonPropertyName("ids")]
    public List<int> Ids { get; set; } = new();
}

public class UnmonitorPayload
{
    [JsonPropertyName("mediaType")]
    public string MediaType { get; set; } = string.Empty;

    [JsonPropertyName("mediaId")]
    public int MediaId { get; set; }

    [JsonPropertyName("seasons")]
    public List<int>? Seasons { get; set; }
}
