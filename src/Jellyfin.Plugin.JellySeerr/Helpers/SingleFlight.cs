using System.Collections.Concurrent;

namespace Jellyfin.Plugin.JellySeerr.Helpers;

/// <summary>Shares active work without sharing a caller's cancellation.</summary>
public sealed class SingleFlight<T>
{
    private readonly ConcurrentDictionary<string, Lazy<Task<T>>> _pending = new(StringComparer.Ordinal);
    public async Task<T> RunAsync(string key, Func<Task<T>> work, CancellationToken cancellationToken = default)
    {
        var lazy = _pending.GetOrAdd(key, _ => new Lazy<Task<T>>(() => Execute(key, work), LazyThreadSafetyMode.ExecutionAndPublication));
        return await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
    private async Task<T> Execute(string key, Func<Task<T>> work)
    {
        try { return await work().ConfigureAwait(false); }
        finally { _pending.TryRemove(key, out _); }
    }
}
