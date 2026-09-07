using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellySeerr.Model;

public class RequestPayload
{
    [JsonPropertyName("MediaType")]
    public string MediaType { get; set; } = string.Empty;

    [JsonPropertyName("mediaType")]
    public string MediaTypeCamel
    {
        get => MediaType;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                MediaType = value;
            }
        }
    }

    [JsonPropertyName("MediaId")]
    public int MediaId { get; set; }

    [JsonPropertyName("mediaId")]
    public int MediaIdCamel
    {
        get => MediaId;
        set
        {
            if (value > 0)
            {
                MediaId = value;
            }
        }
    }

    [JsonPropertyName("ServerId")]
    public int? ServerId { get; set; }

    [JsonPropertyName("ProfileId")]
    public int? ProfileId { get; set; }

    [JsonPropertyName("RootFolder")]
    public string? RootFolder { get; set; }

    [JsonPropertyName("Is4k")]
    public bool Is4k { get; set; }

    [JsonPropertyName("IsAnime")]
    public bool IsAnime { get; set; }

    [JsonPropertyName("Seasons")]
    public List<int>? Seasons { get; set; }

    [JsonPropertyName("Tags")]
    public List<int>? Tags { get; set; }

    [JsonPropertyName("LanguageProfileId")]
    public int? LanguageProfileId { get; set; }
}

public class IssuePayload
{
    [JsonPropertyName("IssueType")]
    public int IssueType { get; set; }

    [JsonPropertyName("Message")]
    public string? Message { get; set; }

    [JsonPropertyName("MediaId")]
    public int MediaId { get; set; }
}

public class BulkCancelPayload
{
    [JsonPropertyName("Ids")]
    public List<int> Ids { get; set; } = new();
}

public class UnmonitorPayload
{
    [JsonPropertyName("MediaType")]
    public string MediaType { get; set; } = string.Empty;

    [JsonPropertyName("MediaId")]
    public int MediaId { get; set; }

    [JsonPropertyName("Seasons")]
    public List<int>? Seasons { get; set; }
}
