using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Helpers;

public static class JsonMemoryCache
{
    private static readonly ConcurrentDictionary<string, Entry> Store = new(StringComparer.Ordinal);

    public static bool TryGet(string key, out JToken? value)
    {
        value = null;
        if (!Store.TryGetValue(key, out Entry? entry))
        {
            return false;
        }

        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            Store.TryRemove(key, out _);
            return false;
        }

        value = ((JToken)entry.Value).DeepClone();
        return true;
    }

    public static bool TryGet<T>(string key, out T? value)
        where T : class
    {
        value = null;
        if (!Store.TryGetValue(key, out Entry? entry) || entry.ExpiresAt <= DateTime.UtcNow)
        {
            if (entry != null)
            {
                Store.TryRemove(key, out _);
            }

            return false;
        }

        if (entry.Value is T typed)
        {
            value = typed;
            return true;
        }

        return false;
    }

    public static void Set(string key, JToken value, TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        Store[key] = new Entry(value.DeepClone(), DateTime.UtcNow.Add(ttl));
    }

    public static void SetObject<T>(string key, T value, TimeSpan ttl)
        where T : class
    {
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        Store[key] = new Entry(value, DateTime.UtcNow.Add(ttl));
    }

    public static void RemoveByPrefix(string prefix)
    {
        foreach (string key in Store.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                Store.TryRemove(key, out _);
            }
        }
    }

    private sealed record Entry(object Value, DateTime ExpiresAt);
}
