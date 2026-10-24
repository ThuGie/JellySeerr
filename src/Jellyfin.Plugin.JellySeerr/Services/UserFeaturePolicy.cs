using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public sealed class NotificationPreferences
{
    public Dictionary<string, int> NotificationTypes { get; set; } = new();
}

public static class UserFeaturePolicy
{
    public static readonly string[] Channels = ["email", "discord", "telegram", "webpush", "pushbullet", "pushover"];
    public const int SupportedEvents = 2 | 4 | 8 | 16 | 64 | 128 | 256 | 512 | 1024 | 2048 | 4096;
    public static JObject NotificationView(JObject source) => new()
    {
        ["channels"] = new JArray(Channels.Select(channel => new JObject {
            ["id"] = channel, ["enabled"] = channel switch {
                "pushbullet" => !string.IsNullOrEmpty(source.Value<string>("pushbulletAccessToken")),
                "pushover" => !string.IsNullOrEmpty(source.Value<string>("pushoverUserKey")),
                "webpush" => source.Value<bool?>("webPushEnabled") == true,
                _ => source.Value<bool?>(channel + "Enabled") == true },
            ["linked"] = channel switch { "discord" => (source["discordIds"] as JArray)?.Count > 0,
                "telegram" => !string.IsNullOrEmpty(source.Value<string>("telegramChatId")), _ => true },
            ["types"] = source["notificationTypes"]?.Value<int?>(channel) ?? (channel is "email" or "webpush" ? SupportedEvents : 0),
            ["allowedTypes"] = channel == "discord" ? source.Value<int?>("discordEnabledTypes") ?? 0 : SupportedEvents }))
    };

    public static JObject MergeNotifications(JObject source, NotificationPreferences payload)
    {
        if (payload.NotificationTypes == null || payload.NotificationTypes.Any(p => !Channels.Contains(p.Key) || p.Value < 0 || (p.Value & ~SupportedEvents) != 0))
            throw new ReleaseSelectionException(400, "Unsupported notification preference.");
        // Seerr's POST replaces channel credentials, so preserve them server-side. Never return these fields to the browser.
        JObject result = (JObject)source.DeepClone();
        JObject types = result["notificationTypes"] as JObject ?? new JObject();
        foreach (var pair in payload.NotificationTypes)
        {
            JObject channel = ((JArray)NotificationView(source)["channels"]!).OfType<JObject>().First(c => c.Value<string>("id") == pair.Key);
            if (channel.Value<bool>("enabled") != true || channel.Value<bool>("linked") != true)
                throw new ReleaseSelectionException(409, "Configure this notification channel in Seerr first.");
            int previous = types.Value<int?>(pair.Key) ?? (pair.Key is "email" or "webpush" ? SupportedEvents : 0);
            int allowed = channel.Value<int>("allowedTypes") & SupportedEvents;
            types[pair.Key] = (previous & ~allowed) | (pair.Value & allowed);
        }
        result["notificationTypes"] = types;
        return result;
    }

    public static JObject? CalendarEvent(JToken? date, DateTime start, DateTime end, string title, string type, int tmdb, string label, JObject request)
    {
        DateTime day;
        if (date?.Type == JTokenType.Date) day = date.Value<DateTime>().Date;
        else
        {
            string value = date?.ToString() ?? "";
            if (value.Length < 10 || !DateTime.TryParseExact(value[..10], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out day)) return null;
        }
        if (day.Date < start.Date || day.Date >= end.Date) return null;
        return new JObject { ["date"] = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), ["title"] = title, ["mediaType"] = type,
            ["tmdbId"] = tmdb, ["label"] = label, ["requestId"] = request["id"], ["requestStatus"] = request["status"],
            ["mediaStatus"] = request["media"]?["status"] };
    }
}
