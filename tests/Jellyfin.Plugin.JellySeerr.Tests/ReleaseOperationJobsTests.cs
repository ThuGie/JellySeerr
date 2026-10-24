using Jellyfin.Plugin.JellySeerr.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Jellyfin.Plugin.JellySeerr.Tests;

public class ReleaseOperationJobsTests
{
    private static ReleaseContext Context() => new(Guid.NewGuid(), JObject.Parse("{id:78,type:'tv',media:{tmdbId:123}}"), new JObject(), false);
    private static ReleaseOperationJobs Store() => new(NullLogger<ReleaseOperationJobs>.Instance);

    [Fact]
    public async Task Slow_search_returns_immediately_and_can_be_polled_until_ready()
    {
        var jobs = Store(); var context = Context();
        var completion = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = jobs.Start(context, "search:test", ct => completion.Task.WaitAsync(ct));
        string token = started.Value<string>("jobToken")!;
        Assert.Equal("running", started.Value<string>("state"));
        Assert.Equal("running", jobs.Get(context.UserId, 78, token).Value<string>("state"));
        completion.SetResult(JObject.Parse("{releases:[],searchToken:'selection'}"));
        var result = await Finish(jobs, context, token);
        Assert.Equal("completed", result.Value<string>("state"));
        Assert.Equal("selection", result["result"]!.Value<string>("searchToken"));
        result["result"]!["searchToken"] = "tampered";
        Assert.Equal("selection", jobs.Get(context.UserId, 78, token)["result"]!.Value<string>("searchToken"));
    }

    [Fact]
    public async Task Repeated_start_reuses_operation_and_cannot_duplicate_a_grab()
    {
        var jobs = Store(); var context = Context(); int calls = 0;
        Task<JObject> Action(CancellationToken _) { Interlocked.Increment(ref calls); return Task.FromResult(new JObject()); }
        var first = jobs.Start(context, "download:selection", Action);
        var second = jobs.Start(context, "download:selection", Action);
        Assert.Equal(first.Value<string>("jobToken"), second.Value<string>("jobToken"));
        await Finish(jobs, context, first.Value<string>("jobToken")!);
        jobs.Start(context, "download:selection", Action);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Polling_is_bound_to_user_and_request()
    {
        var jobs = Store(); var context = Context();
        string token = jobs.Start(context, "search", _ => Task.FromResult(new JObject())).Value<string>("jobToken")!;
        Assert.Throws<ReleaseSelectionException>(() => jobs.Get(Guid.NewGuid(), 78, token));
        Assert.Throws<ReleaseSelectionException>(() => jobs.Get(context.UserId, 79, token));
        Assert.Throws<ReleaseSelectionException>(() => jobs.Get(context.UserId, 78, "unknown"));
    }

    [Fact]
    public async Task Upstream_failure_keeps_diagnostic_status_and_message()
    {
        var jobs = Store(); var context = Context();
        string token = jobs.Start(context, "search", _ => throw new ReleaseSelectionException(502, "Sonarr/Radarr release search returned HTTP 401.")).Value<string>("jobToken")!;
        var result = await Finish(jobs, context, token);
        Assert.Equal("failed", result.Value<string>("state")); Assert.Equal(502, result.Value<int>("status"));
        Assert.Contains("HTTP 401", result.Value<string>("message"));
    }

    [Fact]
    public async Task Unexpected_exception_is_logged_but_does_not_expose_secrets()
    {
        var jobs = Store(); var context = Context();
        string token = jobs.Start(context, "search", _ => throw new InvalidOperationException("secret-api-key")).Value<string>("jobToken")!;
        var result = await Finish(jobs, context, token);
        Assert.Equal("failed", result.Value<string>("state"));
        Assert.DoesNotContain("secret-api-key", result.ToString());
        Assert.Contains("Jellyfin server log", result.Value<string>("message"));
    }

    [Fact]
    public async Task Concurrent_work_is_bounded_per_user()
    {
        var jobs = Store(); var context = Context();
        var completion = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
        var a = jobs.Start(context, "a", ct => completion.Task.WaitAsync(ct));
        var b = jobs.Start(context, "b", ct => completion.Task.WaitAsync(ct));
        Assert.Equal(429, Assert.Throws<ReleaseSelectionException>(() => jobs.Start(context, "c", ct => completion.Task.WaitAsync(ct))).Status);
        completion.SetResult(new JObject());
        await Finish(jobs, context, a.Value<string>("jobToken")!);
        await Finish(jobs, context, b.Value<string>("jobToken")!);
    }

    private static async Task<JObject> Finish(ReleaseOperationJobs jobs, ReleaseContext context, string token)
    {
        using CancellationTokenSource limit = new(TimeSpan.FromSeconds(5));
        while (true)
        {
            JObject snapshot = jobs.Get(context.UserId, context.RequestId, token);
            if (snapshot.Value<string>("state") != "running") return snapshot;
            await Task.Delay(1, limit.Token);
        }
    }
}
