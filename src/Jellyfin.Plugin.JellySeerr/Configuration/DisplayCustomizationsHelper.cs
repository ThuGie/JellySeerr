using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.JellySeerr.Configuration;

// The plugin config.html persists settings set in modals as a JSON string because the XML config store doesn't reliably understand complex objects.
// This helper turns the string into a DisplayCustomizations object.
public static class DisplayCustomizationsHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static DisplayCustomizations Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new DisplayCustomizations();
        }

        try
        {
            return JsonSerializer.Deserialize<DisplayCustomizations>(json, JsonOptions) ?? new DisplayCustomizations();
        }
        catch (JsonException)
        {
            return new DisplayCustomizations();
        }
    }

    public static DisplayCustomizations Resolve(PluginConfiguration config)
    {
        return Deserialize(config.DisplayCustomizationsJson);
    }
}