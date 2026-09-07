using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.JellySeerr.Helpers;

public static class QualityLabelHelper
{
    public static string? Parse(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string text = name.Trim().ToLowerInvariant();
        if (ContainsToken(text, "2160", "3840", "4k", "uhd", "ultra-hd", "ultrahd", "ultra hd"))
        {
            return "4K";
        }

        if (ContainsToken(text, "1440", "2560", "2k", "qhd"))
        {
            return "2K";
        }

        if (ContainsToken(text, "1080", "1920", "fhd", "full hd", "full-hd", "fullhd"))
        {
            return "1080p";
        }

        if (ContainsToken(text, "720", "1280"))
        {
            return "720p";
        }

        if (Regex.IsMatch(text, @"(?:^|[^a-z0-9])(?:576|480|360|sd|dvd|sdtv|ntsc|pal)(?:[^a-z0-9]|$)", RegexOptions.CultureInvariant))
        {
            return "SD";
        }

        return null;
    }

    public static string? FromResolution(int? resolution)
    {
        if (resolution is null or <= 0)
        {
            return null;
        }

        if (resolution >= 2160)
        {
            return "4K";
        }

        if (resolution >= 1440)
        {
            return "2K";
        }

        if (resolution >= 1080)
        {
            return "1080p";
        }

        if (resolution >= 720)
        {
            return "720p";
        }

        return "SD";
    }

    public static string? FromProfile(string? profileName, bool is4k)
    {
        string? parsed = Parse(profileName);
        if (!string.IsNullOrEmpty(parsed))
        {
            return parsed;
        }

        return is4k ? "4K" : null;
    }

    private static bool ContainsToken(string text, params string[] tokens)
    {
        foreach (string token in tokens)
        {
            if (token.Length <= 2)
            {
                if (Regex.IsMatch(text, $@"(?:^|[^a-z0-9]){Regex.Escape(token)}(?:[^a-z0-9]|$)", RegexOptions.CultureInvariant))
                {
                    return true;
                }
            }
            else if (text.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
