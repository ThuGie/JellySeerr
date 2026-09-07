using Jellyfin.Plugin.JellySeerr.Model;

namespace Jellyfin.Plugin.JellySeerr.Helpers;

public static class TransformationPatches
{
    public static string IndexHtml(PatchRequestPayload payload)
    {
        string version = JellySeerrPlugin.Instance.GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        var config = JellySeerrPlugin.Instance.Configuration;
        string cacheParam = config.DeveloperMode
            ? $"?v={version}&t={DateTimeOffset.UtcNow.Ticks}"
            : $"?v={version}&c={config.CacheBustCounter}";

        string cssLinks =
            $"<link rel=\"stylesheet\" href=\"../JellySeerr/jellyseerr-tabs.css{cacheParam}\" />" +
            $"<link rel=\"stylesheet\" href=\"../JellySeerr/jellyseerr-modal.css{cacheParam}\" />" +
            $"<link rel=\"stylesheet\" href=\"../JellySeerr/jellyseerr-requests.css{cacheParam}\" />";
        string scripts =
            $"<script defer src=\"../JellySeerr/jellyseerr-modal.js{cacheParam}\"></script>" +
            $"<script defer src=\"../JellySeerr/jellyseerr-nativeui.js{cacheParam}\"></script>" +
            $"<script defer src=\"../JellySeerr/jellyseerr-tabs.js{cacheParam}\"></script>" +
            $"<script defer src=\"../JellySeerr/jellyseerr-requests.js{cacheParam}\"></script>";

        return payload.Contents!
            .Replace("</head>", $"{cssLinks}</head>", StringComparison.Ordinal)
            .Replace("</body>", $"{scripts}</body>", StringComparison.Ordinal);
    }
}
