using System.Globalization;

namespace Jellyfin.Plugin.JellySeerr.Helpers;

/// <summary>
/// Seerr / Jellyseerr MediaStatus codes.
/// Source of truth: <c>server/constants/media.ts</c> (UNKNOWN=1 … BLOCKLISTED=6, DELETED=7).
/// Do NOT copy enums from <c>seerr-api.yml</c> — OpenAPI still documents <c>6 = DELETED</c> and omits BLOCKLISTED.
/// </summary>
public static class MediaStatusHelper
{
    public const int Unknown = 1;
    public const int Pending = 2;
    public const int Processing = 3;
    public const int PartiallyAvailable = 4;
    public const int Available = 5;
    public const int Blocklisted = 6;
    public const int Deleted = 7;

    /// <summary>
    /// Canonical name → code pairs matching Seerr TypeScript enum declaration order.
    /// </summary>
    public static IReadOnlyDictionary<string, int> CanonicalNameToCode { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["UNKNOWN"] = Unknown,
            ["PENDING"] = Pending,
            ["PROCESSING"] = Processing,
            ["PARTIALLY_AVAILABLE"] = PartiallyAvailable,
            ["AVAILABLE"] = Available,
            ["BLOCKLISTED"] = Blocklisted,
            ["DELETED"] = Deleted
        };

    /// <summary>
    /// Extra aliases accepted from APIs / older clients (still map to Seerr codes).
    /// </summary>
    public static IReadOnlyDictionary<string, int> NameAliases { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["BLACKLISTED"] = Blocklisted,
            ["BLOCKED"] = Blocklisted
        };

    public static int? TryParseCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string key = raw.Trim();
        if (CanonicalNameToCode.TryGetValue(key, out int code))
        {
            return code;
        }

        if (NameAliases.TryGetValue(key, out code))
        {
            return code;
        }

        return int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric)
            ? numeric
            : null;
    }

    /// <summary>
    /// Normalize a status token to a digit string for provider ids / caches.
    /// </summary>
    public static string? NormalizeToCodeString(string? raw)
    {
        int? code = TryParseCode(raw);
        return code?.ToString(CultureInfo.InvariantCulture);
    }

    public static string GetLabel(int? mediaStatus, int? requestStatus = null)
    {
        if (requestStatus == 4 && mediaStatus is not (PartiallyAvailable or Available))
        {
            return "Failed";
        }

        return mediaStatus switch
        {
            Blocklisted => "Blocklisted",
            Deleted => "Deleted",
            Available => "Available",
            PartiallyAvailable => "Partially Available",
            Processing => "Processing",
            Pending => "Pending",
            _ => requestStatus switch
            {
                5 => "Completed",
                4 => "Failed",
                3 => "Declined",
                2 => "Approved",
                1 => "Pending Approval",
                _ => "Unknown"
            }
        };
    }
}
