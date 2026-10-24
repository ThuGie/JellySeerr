using System.Net;
using System.Text;
using Jellyfin.Plugin.JellySeerr.Model;
using Jellyfin.Plugin.JellySeerr.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Jellyfin.Plugin.JellySeerr.Tests;

public class AvailabilityTests
{
    [Theory]
    [InlineData("downloading", "downloading", "Downloading")]
    [InlineData("completed", "importPending", "Waiting for import")]
    [InlineData("completed", "importBlocked", "Import blocked")]
    [InlineData("failed", "failedPending", "Download failed")]
    [InlineData("paused", "downloading", "Download paused")]
    [InlineData("stalled", "downloading", "Download stalled")]
    public void Queue_state_is_explained_without_exposing_internal_paths(string status, string state, string expected)
    {
        JObject q = new() { ["status"] = status, ["trackedDownloadState"] = state,
            ["outputPath"] = "/private/downloads", ["apiKey"] = "secret" };
        JObject view = AvailabilityPolicy.Queue(q);
        Assert.Equal(expected, view.Value<string>("statusLabel"));
        Assert.DoesNotContain("private", view.ToString());
        Assert.DoesNotContain("secret", view.ToString());
    }

    [Fact]
    public async Task Overview_distinguishes_episode_states_and_only_exposes_approved_seasons()
    {
        using var f = new Fixture();
        JObject view = await f.Service.AvailabilityAsync(f.Context, default);
        var states = ((JArray)view["episodes"]!).OfType<JObject>().ToDictionary(e => e.Value<int>("id"), e => e.Value<string>("statusKey"));
        Assert.Equal("available", states[21]);
        Assert.Equal("missing", states[22]);
        Assert.Equal("downloading", states[23]);
        Assert.Equal("unaired", states[24]);
        Assert.Equal("unmonitored", states[25]);
        Assert.Equal("unknown", states[26]);
        Assert.Equal("import-blocked", states[27]);
        Assert.DoesNotContain(28, states.Keys);
        Assert.DoesNotContain("/private", view.ToString());
    }

    [Fact]
    public async Task Missing_search_only_dispatches_aired_monitored_missing_unqueued_episodes()
    {
        using var f = new Fixture();
        JObject result = await f.Service.SearchMissingAsync(f.Context, new() { SeasonNumber = 1 }, default);
        Assert.Equal(new[] { 22 }, ((JArray)result["searchedEpisodeIds"]!).Values<int>());
        Assert.Equal("EpisodeSearch", f.Posted!.Value<string>("name"));
        Assert.Equal(new[] { 22 }, ((JArray)f.Posted["episodeIds"]!).Values<int>());
        Assert.Equal(new[] { "POST command" }, f.Mutations);
        JObject overview = await f.Service.AvailabilityAsync(f.Context, default);
        JObject searched = ((JArray)overview["episodes"]!).OfType<JObject>().Single(e => e.Value<int>("id") == 22);
        Assert.Equal("search-requested", searched.Value<string>("statusKey"));
        Assert.False(searched.Value<bool>("canSearch"));
    }

    [Fact]
    public async Task Explicit_selection_cannot_include_episodes_from_another_season()
    {
        using var f = new Fixture();
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.SearchMissingAsync(f.Context,
            new() { SeasonNumber = 1, EpisodeIds = [22, 28] }, default));
        Assert.Empty(f.Mutations);
    }

    [Fact]
    public async Task Existing_files_are_rechecked_instead_of_using_the_overview_snapshot()
    {
        using var f = new Fixture();
        await f.Service.AvailabilityAsync(f.Context, default);
        f.MissingHasFile = true;
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.SearchMissingAsync(f.Context,
            new() { SeasonNumber = 1, EpisodeIds = [22] }, default));
        Assert.Empty(f.Mutations);
    }

    [Fact]
    public async Task Shared_season_view_does_not_grant_individual_search_permission()
    {
        using var f = new Fixture();
        var shared = f.Context with { CanChooseEpisodes = false };
        JObject view = await f.Service.AvailabilityAsync(shared, default);
        Assert.False(view.Value<bool>("canSearchMissing"));
        Assert.All(((JArray)view["episodes"]!).OfType<JObject>(), e => Assert.False(e.Value<bool>("canSearch")));
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.SearchMissingAsync(shared, new() { SeasonNumber = 1 }, default));
        Assert.Empty(f.Mutations);
    }

    [Theory]
    [InlineData(1, "pending")]
    [InlineData(3, "declined")]
    [InlineData(4, "request-failed")]
    public async Task Pending_declined_and_failed_requests_need_no_Arr_lookup(int status, string expected)
    {
        using var f = new Fixture();
        f.Context.Request["status"] = status;
        JObject view = await f.Service.AvailabilityAsync(f.Context, default);
        Assert.Equal(expected, view.Value<string>("statusKey"));
        Assert.Empty(f.Calls);
    }

    [Fact]
    public async Task Repeated_searches_are_throttled_even_if_the_command_response_is_uncertain()
    {
        using var f = new Fixture { FailCommand = true };
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.SearchMissingAsync(f.Context, new() { SeasonNumber = 1 }, default));
        ReleaseSelectionException ex = await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.SearchMissingAsync(f.Context, new() { SeasonNumber = 1 }, default));
        Assert.Equal(429, ex.Status);
        Assert.Single(f.Mutations);
    }

    [Fact]
    public async Task Queue_is_paginated_and_an_episode_on_later_pages_is_not_searched()
    {
        using var f = new Fixture { QueuedOnSecondPage = true };
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.SearchMissingAsync(f.Context, new() { SeasonNumber = 1 }, default));
        Assert.Contains("queue?page=2&pageSize=250", f.Calls);
        Assert.Empty(f.Mutations);
    }

    [Fact]
    public async Task Disabled_series_monitoring_never_changes_monitoring_or_starts_search()
    {
        using var f = new Fixture { Monitored = false };
        JObject view = await f.Service.AvailabilityAsync(f.Context, default);
        Assert.False(view.Value<bool>("canSearchMissing"));
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.SearchMissingAsync(f.Context, new() { SeasonNumber = 1 }, default));
        Assert.Empty(f.Mutations);
    }

    [Fact]
    public void Viewing_shared_tv_status_does_not_allow_viewing_another_users_movie_request()
    {
        JObject request = JObject.Parse("{status:1,type:'tv',requestedBy:{id:8}}");
        Assert.True(AvailabilityPolicy.CanView(request, 7, false));
        request["type"] = "movie";
        Assert.False(AvailabilityPolicy.CanView(request, 7, false));
        Assert.True(AvailabilityPolicy.CanView(request, 7, true));
    }

    [Theory]
    [InlineData(true, true, true, "available")]
    [InlineData(false, false, true, "unmonitored")]
    [InlineData(false, true, false, "unreleased")]
    [InlineData(false, true, true, "missing")]
    public async Task Movie_overview_explains_missing_files_monitoring_and_release_state(bool hasFile, bool monitored, bool released, string expected)
    {
        using var f = new Fixture();
        f.Context.Request["type"] = "movie";
        f.Movie["movieFileId"] = hasFile ? 9 : 0;
        f.Movie["monitored"] = monitored;
        f.Movie["isAvailable"] = released;
        JObject view = await f.Service.AvailabilityAsync(f.Context, default);
        Assert.Equal(expected, view.Value<string>("statusKey"));
        Assert.False(view.Value<bool>("isTv"));
    }

    [Theory]
    [InlineData(10, "unaired")]
    [InlineData(-10, "missing")]
    public void Air_dates_remain_utc_when_Newtonsoft_parses_them_as_date_tokens(int minutes, string expected)
    {
        var now = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        JObject episode = new() { ["id"] = 1, ["seriesId"] = 5, ["episodeFileId"] = 0,
            ["monitored"] = true, ["airDateUtc"] = now.AddMinutes(minutes).UtcDateTime };
        Assert.Equal(expected, AvailabilityPolicy.Episode(episode, [], now, true).Value<string>("statusKey"));
    }

    private sealed class Fixture : HttpMessageHandler, IHttpClientFactory
    {
        public ServarrReleaseService Service { get; }
        public ReleaseContext Context { get; } = new(Guid.NewGuid(),
            JObject.Parse("{id:42,type:'tv',status:2,media:{tmdbId:123,tvdbId:456},seasons:[{seasonNumber:1,status:2}]}"),
            JObject.Parse("{id:0,hostname:'localhost',port:8989,apiKey:'secret'}"), false);
        public List<string> Calls { get; } = [];
        public List<string> Mutations { get; } = [];
        public JObject? Posted { get; private set; }
        public bool MissingHasFile, QueuedOnSecondPage, FailCommand;
        public bool Monitored = true;
        public JObject Movie { get; } = JObject.Parse("{id:5,title:'Movie',tmdbId:123}");
        public Fixture() => Service = new(this, NullLogger<ServarrReleaseService>.Instance);
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.PathAndQuery.Replace("/api/v3/", "");
            Calls.Add(path);
            if (request.Method != HttpMethod.Get)
            {
                Mutations.Add(request.Method + " " + path);
                Posted = JObject.Parse(await request.Content!.ReadAsStringAsync(ct));
                return Json(new JObject(), FailCommand ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Created);
            }
            if (path == "movie?tmdbId=123") return Json(new JArray(Movie));
            if (path == "series") return Json(new JArray(new JObject
                { ["id"] = 5, ["title"] = "Series", ["tmdbId"] = 123, ["tvdbId"] = 456, ["monitored"] = Monitored }));
            if (path.StartsWith("episode?"))
            {
                JArray all = new(Enumerable.Range(21, 8).Select(id => new JObject
                {
                    ["id"] = id, ["seriesId"] = 5, ["seasonNumber"] = id == 28 ? 2 : 1,
                    ["episodeNumber"] = id - 20, ["title"] = "Episode " + id, ["monitored"] = id != 25,
                    ["episodeFileId"] = id == 21 || id == 22 && MissingHasFile ? 9 : 0,
                    ["airDateUtc"] = id == 26 ? null : id == 24 ? "9999-01-01T00:00:00Z" : "2020-01-01T00:00:00Z"
                }));
                return Json(all);
            }
            if (path.StartsWith("queue?"))
            {
                JArray batch = path.Contains("page=2&") ? new JArray(new JObject { ["episodeId"] = 22, ["seriesId"] = 5, ["status"] = "downloading" })
                    : JArray.Parse("[{episodeId:23,seriesId:5,status:'downloading'},"
                        + "{episodeId:27,seriesId:5,status:'completed',trackedDownloadState:'importBlocked',outputPath:'/private/downloads'}]");
                return Json(new JObject { ["records"] = batch, ["totalRecords"] = QueuedOnSecondPage ? 3 : 2 });
            }
            throw new InvalidOperationException(path);
        }
        private static HttpResponseMessage Json(JToken content, HttpStatusCode status = HttpStatusCode.OK) => new(status)
            { Content = new StringContent(content.ToString(), Encoding.UTF8, "application/json") };
    }
}
