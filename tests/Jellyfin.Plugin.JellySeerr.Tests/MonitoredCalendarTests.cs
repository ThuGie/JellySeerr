using System.Net;
using System.Text;
using Jellyfin.Plugin.JellySeerr.Services;
using Jellyfin.Plugin.JellySeerr.Configuration;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Jellyfin.Plugin.JellySeerr.Tests;

public class MonitoredCalendarTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddDays(42);
    private static JArray Shows() => JArray.Parse("[{id:1,title:'Shared Show',tmdbId:123,monitored:true,status:'continuing',nextAiring:'2026-10-24T20:00:00Z',seasons:[{seasonNumber:1,monitored:true}]},{id:2,title:'Unmonitored',monitored:false,status:'continuing'},{id:3,title:'Awaiting',monitored:true,status:'continuing'},{id:4,title:'Ended',monitored:true,status:'ended'},{id:5,title:'Disabled seasons',monitored:true,status:'continuing',seasons:[{seasonNumber:1,monitored:false}]}]");
    private static JArray Episodes() => JArray.Parse("[{id:10,seriesId:1,monitored:true,seasonNumber:1,episodeNumber:1,title:'Episode',airDateUtc:'2026-10-24T20:00:00Z',hasFile:false},{id:11,seriesId:2,monitored:true,seasonNumber:1,episodeNumber:1,airDateUtc:'2026-10-24T20:00:00Z'},{id:12,seriesId:1,monitored:false,seasonNumber:1,episodeNumber:2,airDateUtc:'2026-10-25T20:00:00Z'}]");

    [Fact]
    public void Calendar_uses_monitored_library_regardless_of_request_owner()
    {
        var data = MonitoredCalendarService.Build(Shows(), Episodes(), Start, End, Start);
        var item = Assert.Single((JArray)data["events"]!);
        Assert.Equal("Shared Show", item.Value<string>("title")); Assert.Equal(123, item.Value<int>("tmdbId"));
        Assert.False(item.Value<bool>("hasFile"));
        Assert.Equal("Awaiting", Assert.Single((JArray)data["awaitingDates"]!).Value<string>("title"));
    }

    [Fact]
    public void Calendar_excludes_unmonitored_seasons_and_out_of_range_episodes()
    {
        var shows = Shows(); shows[0]!["seasons"]![0]!["monitored"] = false;
        Assert.Empty((JArray)MonitoredCalendarService.Build(shows, Episodes(), Start, End, Start)["events"]!);
        var episodes = Episodes(); episodes[0]!["airDateUtc"] = End.ToString("O");
        Assert.Empty((JArray)MonitoredCalendarService.Build(Shows(), episodes, Start, End, Start)["events"]!);
    }

    [Fact]
    public void Existing_files_remain_visible_and_are_labeled_available()
    {
        var episodes = Episodes(); episodes[0]!["hasFile"] = true;
        var data = MonitoredCalendarService.Build(Shows(), episodes, Start, End, Start);
        Assert.True(Assert.Single((JArray)data["events"]!).Value<bool>("hasFile"));
    }

    [Theory]
    [InlineData("nl-NL")]
    [InlineData("en-US")]
    public void Sonarr_dates_are_parsed_independently_of_server_regional_settings(string locale)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(locale);
            var result = MonitoredCalendarService.Build(Shows(), Episodes(), Start, End, Start);
            var date = Assert.Single((JArray)result["events"]!).Value<DateTimeOffset>("airDateUtc");
            Assert.Equal(new DateTimeOffset(2026, 10, 24, 20, 0, 0, TimeSpan.Zero), date);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task Navigating_away_does_not_cancel_another_viewers_shared_calendar_lookup()
    {
        using var f = new Fixture { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var service = new MonitoredCalendarService(f);
        var servers = new JArray(Server(Guid.NewGuid().ToString("N") + ".test"));
        using CancellationTokenSource canceled = new();
        var first = service.GetAsync(servers, Start, End, canceled.Token);
        var second = service.GetAsync(servers, Start, End, default);
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        f.Gate.SetResult(true);
        var data = await second;
        Assert.Single((JArray)data["events"]!); Assert.Equal(2, f.Calls);
    }

    [Fact]
    public async Task Multiple_instances_return_partial_results_and_no_connection_secrets()
    {
        using var f = new Fixture();
        var service = new MonitoredCalendarService(f);
        var servers = new JArray(Server("good.test"), Server("bad.test"));
        var data = await service.GetAsync(servers, Start, End, default);
        Assert.Single((JArray)data["events"]!); Assert.Single((JArray)data["failures"]!);
        Assert.Equal(1, data.Value<int>("serverCount"));
        Assert.DoesNotContain("secret", data.ToString()); Assert.DoesNotContain("/private/path", data.ToString());
        Assert.DoesNotContain("downloadUrl", data.ToString());
    }

    [Fact]
    public async Task Calendar_is_cached_and_inflight_lookups_are_shared()
    {
        using var f = new Fixture();
        var service = new MonitoredCalendarService(f);
        var servers = new JArray(Server(Guid.NewGuid().ToString("N") + ".test"));
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.GetAsync(servers, Start, End, default)));
        await service.GetAsync(servers, Start, End, default);
        Assert.Equal(2, f.Calls); Assert.All(results, r => Assert.Single((JArray)r["events"]!));
    }

    [Fact]
    public async Task Invalid_range_and_failed_connections_give_actionable_errors()
    {
        using var f = new Fixture(); var service = new MonitoredCalendarService(f);
        Assert.Equal(400, (await Assert.ThrowsAsync<ReleaseSelectionException>(() => service.GetAsync([], End, Start, default))).Status);
        Assert.Equal(400, (await Assert.ThrowsAsync<ReleaseSelectionException>(() => service.GetAsync([], Start, Start.AddDays(50), default))).Status);
        var error = await Assert.ThrowsAsync<ReleaseSelectionException>(() => service.GetAsync(new JArray(Server("bad.test")), Start, End, default));
        Assert.Contains("addresses and API keys", error.Message);
    }

    [Fact]
    public void Existing_tab_configuration_gains_calendar_and_can_disable_it()
    {
        var tabs = TabConfigHelper.Normalize([new TabConfig { Id = "requests" }]);
        Assert.True(tabs.Single(t => t.Id == "calendar").Enabled);
        tabs = TabConfigHelper.Normalize([new TabConfig { Id = "calendar", Enabled = false }]);
        Assert.False(tabs.Single(t => t.Id == "calendar").Enabled);
        Assert.Contains("js:calendar", TabConfigHelper.NormalizeBarOrder([]));
    }

    private static JObject Server(string host) => new() { ["name"] = host == "bad.test" ? "Offline instance" : "Main", ["hostname"] = host, ["port"] = 8989, ["apiKey"] = "secret-key" };
    private sealed class Fixture : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls;
        public TaskCompletionSource<bool>? Gate;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls); await Task.Yield();
            if (Gate != null) await Gate.Task.WaitAsync(ct);
            if (request.RequestUri!.Host == "bad.test") return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            Assert.Equal("secret-key", request.Headers.GetValues("X-Api-Key").Single());
            JArray data = request.RequestUri.AbsolutePath.EndsWith("/series") ? Shows() : Episodes();
            if (request.RequestUri.AbsolutePath.EndsWith("/calendar")) Assert.Contains("unmonitored=false", request.RequestUri.Query);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(data.ToString(), Encoding.UTF8, "application/json") };
        }
    }
}
