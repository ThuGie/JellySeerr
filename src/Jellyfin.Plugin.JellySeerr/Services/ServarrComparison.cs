using Newtonsoft.Json.Linq;
namespace Jellyfin.Plugin.JellySeerr.Services;
public partial class ServarrReleaseService
{
    private static JObject FileComparison(JObject? file)
    {
        JObject result = new() { ["quality"] = file?["quality"]?["quality"]?["name"], ["size"] = file?["size"] };
        foreach (string field in new[] { "videoCodec", "audioCodec", "audioChannels", "audioLanguages", "subtitles", "videoDynamicRangeType", "videoBitDepth", "width", "height" })
            result[field] = file?["mediaInfo"]?[field]?.DeepClone();
        result["languages"] = file?["languages"]?.DeepClone();
        return result;
    }
}
