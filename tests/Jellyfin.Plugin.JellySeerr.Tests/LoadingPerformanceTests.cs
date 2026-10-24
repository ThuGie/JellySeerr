using System.Net;
using System.Reflection;
using Jellyfin.Plugin.JellySeerr.Configuration;
using Jellyfin.Plugin.JellySeerr.Helpers;
using Jellyfin.Plugin.JellySeerr.Services;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Jellyfin.Plugin.JellySeerr.Tests;

public class LoadingPerformanceTests
{
    [Fact]
    public async Task Concurrent_work_is_started_once_and_cancelled_waiter_does_not_cancel_others()
    {
        SingleFlight<int> flight = new();
        TaskCompletionSource<int> finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        Task<int> Work() { Interlocked.Increment(ref calls); return finish.Task; }
        using CancellationTokenSource cancellation = new();
        Task<int> cancelled = flight.RunAsync("same", Work, cancellation.Token);
        Task<int>[] callers = Enumerable.Range(0, 100).Select(_ => flight.RunAsync("same", Work)).ToArray();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.Equal(1, calls);
        finish.SetResult(42);
        Assert.All(await Task.WhenAll(callers), value => Assert.Equal(42, value));
        Assert.Equal(99, await flight.RunAsync("same", () => Task.FromResult(99)));
    }

    [Fact]
    public async Task Failed_shared_lookup_can_be_retried()
    {
        SingleFlight<int> flight = new();
        await Assert.ThrowsAsync<InvalidOperationException>(() => flight.RunAsync("same", () => throw new InvalidOperationException()));
        Assert.Equal(1, await flight.RunAsync("same", () => Task.FromResult(1)));
    }

    [Fact]
    public async Task Bounded_fetches_run_in_parallel_without_overloading_upstream_and_preserve_order()
    {
        TaskCompletionSource allStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int active = 0, peak = 0;
        Task<int[]> work = BoundedAsync.SelectAsync(Enumerable.Range(0, 20), 4, async id =>
        {
            int count = Interlocked.Increment(ref active);
            lock (finish) peak = Math.Max(peak, count);
            if (count == 4) allStarted.TrySetResult();
            await finish.Task;
            Interlocked.Decrement(ref active);
            return id;
        });
        await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, peak);
        finish.SetResult();
        Assert.Equal(Enumerable.Range(0, 20), await work);
        Assert.Equal(4, peak);
    }

    [Fact]
    public void Cache_scope_isolates_server_credentials_and_user()
    {
        using HttpClient a = PooledHttpClients.Create(new Uri("https://one.example"));
        using HttpClient b = PooledHttpClients.Create(new Uri("https://one.example"));
        a.DefaultRequestHeaders.Add("X-Api-Key", "secret");
        b.DefaultRequestHeaders.Add("X-Api-Key", "secret");
        Assert.Equal(ApiCacheScope.ForClient(a), ApiCacheScope.ForClient(b));
        a.DefaultRequestHeaders.Add("X-Api-User", "7");
        Assert.NotEqual(ApiCacheScope.ForClient(a), ApiCacheScope.ForClient(b));
        Assert.False(b.DefaultRequestHeaders.Contains("X-Api-User"));
        b.BaseAddress = new Uri("https://two.example");
        Assert.DoesNotContain("secret", ApiCacheScope.ForClient(a));
        Assert.NotEqual(ApiCacheScope.ForClient(a), ApiCacheScope.ForClient(b));
    }

    [Fact]
    public void Json_cache_returns_independent_data_and_does_not_keep_unbounded_entries()
    {
        string prefix = Guid.NewGuid().ToString("N");
        try
        {
            JObject original = JObject.Parse("{value:1}");
            JsonMemoryCache.Set(prefix + "clone", original, TimeSpan.FromMinutes(1));
            original["value"] = 2;
            Assert.True(JsonMemoryCache.TryGet(prefix + "clone", out JToken? read));
            Assert.Equal(1, read!.Value<int>("value"));
            read["value"] = 3;
            Assert.True(JsonMemoryCache.TryGet(prefix + "clone", out read));
            Assert.Equal(1, read!.Value<int>("value"));
            for (int i = 0; i < 2200; i++) JsonMemoryCache.Set(prefix + i, new JValue(i), TimeSpan.FromMinutes(1));
            Assert.False(JsonMemoryCache.TryGet(prefix + "0", out _));
            Assert.True(JsonMemoryCache.TryGet(prefix + "2199", out _));
        }
        finally { JsonMemoryCache.RemoveByPrefix(prefix); }
    }

    [Fact]
    public async Task Registering_twenty_cards_does_not_download_images_and_concurrent_image_requests_share_one_fetch()
    {
        using ImageFixture f = new();
        for (int i = 0; i < 40; i++) f.Service.RegisterImage($"https://images.test/{i}.jpg");
        Assert.Equal(0, f.Handler.Count);
        string key = f.Service.RegisterImage("https://images.test/0.jpg");
        var pending = Enumerable.Range(0, 20).Select(_ => f.Service.GetCachedImageFileAsync(key)).ToArray();
        Assert.Equal(1, f.Handler.Count);
        f.Handler.Ready.SetResult();
        var images = await Task.WhenAll(pending);
        Assert.All(images, image => Assert.NotNull(image));
        Assert.Equal(1, f.Handler.Count);
        Assert.NotNull(await f.Service.GetCachedImageFileAsync(key));
        Assert.Equal(1, f.Handler.Count);
        Assert.Null(await f.Service.GetCachedImageFileAsync("unknown-key"));
    }

    [Fact]
    public async Task Expired_image_is_still_available_when_upstream_refresh_fails()
    {
        using ImageFixture f = new(cacheSeconds: -1);
        f.Handler.Ready.SetResult();
        string key = f.Service.RegisterImage("https://images.test/stale.jpg");
        Assert.NotNull(await f.Service.GetCachedImageFileAsync(key));
        f.Handler.Fail = true;
        var stale = await f.Service.GetCachedImageFileAsync(key);
        Assert.NotNull(stale);
        Assert.Equal(0, stale!.MaxAgeSeconds);
        Assert.Equal(2, f.Handler.Count);
    }

    public class PathsProxy : DispatchProxy
    {
        public string Root { get; set; } = "";
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Root;
    }

    private sealed class ImageFixture : IDisposable
    {
        public ImageHandler Handler { get; } = new();
        public ImageCacheService Service { get; }
        private readonly string _root = Path.Combine(Path.GetTempPath(), "JellySeerr.LoadingTests", Guid.NewGuid().ToString("N"));
        public ImageFixture(int cacheSeconds = 3600)
        {
            IApplicationPaths paths = DispatchProxy.Create<IApplicationPaths, PathsProxy>();
            ((PathsProxy)paths).Root = _root;
            Service = new ImageCacheService(NullLogger<ImageCacheService>.Instance, paths, new HttpClient(Handler),
                () => new PluginConfiguration { CacheTimeoutSeconds = cacheSeconds });
        }
        public void Dispose()
        {
            Service.Dispose();
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "JellySeerr.LoadingTests")) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(_root).StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }
    }

    private sealed class ImageHandler : HttpMessageHandler
    {
        public int Count;
        public bool Fail;
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Count);
            await Ready.Task;
            return new HttpResponseMessage(Fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
                { Content = new ByteArrayContent([1, 2, 3, 4]) };
        }
    }
}
