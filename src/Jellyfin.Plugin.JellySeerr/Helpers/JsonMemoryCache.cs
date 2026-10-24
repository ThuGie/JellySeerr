using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Helpers;

public static class JsonMemoryCache
{
    private static readonly ConcurrentDictionary<string, Entry> Store = new(StringComparer.Ordinal);
    private static int _writes;
    private static readonly object PruneLock = new();

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

        if (entry.Value is not JToken token) return false;
        value = token.DeepClone();
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
        Prune();
    }

    public static void SetObject<T>(string key, T value, TimeSpan ttl)
        where T : class
    {
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        Store[key] = new Entry(value, DateTime.UtcNow.Add(ttl));
        Prune();
    }

    private static void Prune()
    {
        if (Interlocked.Increment(ref _writes) % 64 != 0 && Store.Count < 2048) return;
        lock (PruneLock)
        {
            DateTime now = DateTime.UtcNow;
            foreach (var pair in Store.Where(p => p.Value.ExpiresAt <= now).ToArray())
                ((ICollection<KeyValuePair<string, Entry>>)Store).Remove(pair);
            if (Store.Count >= 2048)
                foreach (var pair in Store.OrderBy(p => p.Value.ExpiresAt).Take(Store.Count - 1792).ToArray())
                    ((ICollection<KeyValuePair<string, Entry>>)Store).Remove(pair);
        }
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
