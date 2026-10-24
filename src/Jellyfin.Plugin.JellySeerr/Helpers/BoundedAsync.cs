namespace Jellyfin.Plugin.JellySeerr.Helpers;

public static class BoundedAsync
{
    public static async Task<TResult[]> SelectAsync<T, TResult>(IEnumerable<T> items, int concurrency,
        Func<T, Task<TResult>> work, CancellationToken cancellationToken = default)
    {
        using SemaphoreSlim gate = new(Math.Max(1, concurrency));
        return await Task.WhenAll(items.Select(async item =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { return await work(item).ConfigureAwait(false); }
            finally { gate.Release(); }
        })).ConfigureAwait(false);
    }
}
