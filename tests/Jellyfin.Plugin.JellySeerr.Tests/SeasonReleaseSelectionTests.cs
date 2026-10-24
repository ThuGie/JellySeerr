using System.Net;
using System.Text;
using Jellyfin.Plugin.JellySeerr.Model;
using Jellyfin.Plugin.JellySeerr.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Jellyfin.Plugin.JellySeerr.Tests;

public class SeasonReleaseSelectionTests
{
    [Theory]
    [InlineData("tv", 2, true)]
    [InlineData("tv", 5, true)]
    [InlineData("tv", 1, false)]
    [InlineData("tv", 3, false)]
    [InlineData("movie", 2, false)]
    public void Other_users_can_replace_only_approved_tv_seasons(string type, int status, bool expected)
    {
        JObject request = new() { ["type"] = type, ["status"] = status, ["requestedBy"] = new JObject { ["id"] = 8 } };
        Assert.Equal(expected, ReleaseSelectionPolicy.CanAccess(request, 7, false, sharedSeason: true));
        Assert.False(ReleaseSelectionPolicy.CanAccess(request, 7, false));
    }

    [Fact]
    public async Task Shared_season_replacement_deletes_only_selected_season_files_once()
    {
        using var f = new Fixture();
        JObject search = await f.Search();
        Assert.True(search["releases"]![0]!.Value<bool>("selectable"));
        Assert.Equal(3, ((JArray)search["affectedEpisodes"]!).Count);
        Assert.Equal(2, search.Value<int>("fileCount"));
        Assert.DoesNotContain("downloadUrl", search.ToString());
        await f.Download(search, reject: true);
        Assert.Equal(new[] { "POST release", "POST history/failed/100", "DELETE episodefile/9", "DELETE episodefile/10" }, f.Mutations);
        Assert.Equal(5, f.Posted!.Value<int>("seriesId"));
        Assert.Null(f.Posted["episodeId"]);
        Assert.Null(f.Posted["shouldOverride"]);
    }

    [Fact]
    public async Task Options_include_shared_seasons_but_do_not_grant_episode_access()
    {
        using var f = new Fixture();
        JObject options = await f.Service.OptionsAsync(f.Context, default);
        Assert.False(options.Value<bool>("canChooseEpisodes"));
        Assert.Single((JArray)options["seasons"]!);
        Assert.Equal(1, options["seasons"]![0]!.Value<int>("seasonNumber"));
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.SearchAsync(f.Context, 21, default));
        JObject search = await f.Search();
        var episodePayload = f.Payload(search);
        episodePayload.SeasonNumber = null;
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.DownloadAsync(f.Context, episodePayload, default));
        Assert.Empty(f.Mutations);
    }

    [Fact]
    public async Task Incomplete_pack_is_labeled_and_cannot_remove_existing_episodes()
    {
        using var f = new Fixture { Incomplete = true };
        JObject search = await f.Search();
        Assert.False(search["releases"]![0]!.Value<bool>("selectable"));
        Assert.Contains("2 of 3", search["releases"]![0]!.Value<string>("note"));
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Download(search));
        Assert.Empty(f.Mutations);
    }

    [Theory]
    [InlineData("multi-season")]
    [InlineData("wrong-series")]
    [InlineData("unknown-coverage")]
    [InlineData("single-episode")]
    [InlineData("mapped-other-season")]
    public async Task Pack_scope_must_be_verified_before_grabbing(string problem)
    {
        using var f = new Fixture { ScopeProblem = problem };
        JObject search = await f.Search();
        Assert.False(search["releases"]![0]!.Value<bool>("selectable"));
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Download(search));
        Assert.Empty(f.Mutations);
    }

    [Fact]
    public async Task Season_upgrade_rejection_can_be_relaxed_but_profile_rejections_cannot()
    {
        using var f = new Fixture { Rejections = new JArray("Season pack does not meet the upgrade criteria. Upgradable: 0/3") };
        JObject search = await f.Search();
        Assert.True(search["releases"]![0]!.Value<bool>("selectable"));
        f.Rejections.Add("Quality is not wanted in profile");
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Download(search));
        Assert.Empty(f.Mutations);
    }

    [Fact]
    public async Task Failed_pack_grab_keeps_all_old_files_and_releases()
    {
        using var f = new Fixture { FailGrab = true };
        JObject search = await f.Search();
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Download(search, true));
        Assert.Equal(new[] { "POST release" }, f.Mutations);
    }

    [Fact]
    public async Task Changed_file_or_revoked_season_approval_requires_new_search()
    {
        using var f = new Fixture();
        JObject search = await f.Search();
        f.FirstFileId = 99;
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Download(search));
        Assert.Empty(f.Mutations);
        f.FirstFileId = 9;
        f.Context.Request["seasons"]![0]!["status"] = 3;
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Download(search));
        Assert.Empty(f.Mutations);
    }

    [Fact]
    public async Task Files_shared_with_another_season_cannot_be_removed()
    {
        using var f = new Fixture { CrossSeasonFile = true };
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Search());
        Assert.Empty(f.Mutations);
    }

    [Fact]
    public async Task Cleanup_skips_files_already_replaced_by_import()
    {
        using var f = new Fixture { ImportOnGrab = true };
        await f.Download(await f.Search());
        Assert.Equal(new[] { "POST release", "DELETE episodefile/10" }, f.Mutations);
    }

    [Fact]
    public async Task Partial_cleanup_failure_continues_and_reports_queued_state()
    {
        using var f = new Fixture { FailFirstDelete = true };
        JObject result = await f.Download(await f.Search());
        Assert.Contains("queued, but cleanup", result.Value<string>("message"));
        Assert.Contains("DELETE episodefile/10", f.Mutations);
    }

    [Fact]
    public async Task Unknown_history_disables_blocklisting_but_allows_plain_replacement()
    {
        using var f = new Fixture { UnknownHistory = true };
        JObject search = await f.Search();
        Assert.False(search.Value<bool>("canRejectCurrent"));
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Download(search, true));
        Assert.Empty(f.Mutations);
        await f.Download(search);
        Assert.DoesNotContain(f.Mutations, x => x.Contains("history/failed"));
    }

    [Fact]
    public async Task Tokens_cannot_switch_seasons_users_or_be_replayed()
    {
        using var f = new Fixture();
        JObject search = await f.Search();
        var wrongSeason = f.Payload(search);
        wrongSeason.SeasonNumber = 2;
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.DownloadAsync(f.Context, wrongSeason, default));
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.DownloadAsync(f.Context with { UserId = Guid.NewGuid() }, f.Payload(search), default));
        Assert.Empty(f.Mutations);
        await f.Download(search);
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Download(search));
        Assert.Single(f.Mutations, x => x == "POST release");
    }

    [Fact]
    public async Task Queue_overlap_is_checked_for_any_episode_in_the_season()
    {
        using var f = new Fixture { QueuedEpisode = 23 };
        JObject search = await f.Search();
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Download(search));
        Assert.Empty(f.Mutations);
        f.QueuedEpisode = 24; // another season is unaffected
        await f.Download(search);
        Assert.Contains("POST release", f.Mutations);
    }

    private sealed class Fixture : HttpMessageHandler, IHttpClientFactory
    {
        public ServarrReleaseService Service { get; }
        public ReleaseContext Context { get; }
        public List<string> Mutations { get; } = [];
        public JObject? Posted { get; private set; }
        public bool Incomplete { get; set; }
        public string? ScopeProblem { get; set; }
        public JArray Rejections { get; set; } = [];
        public bool FailGrab { get; set; }
        public bool FailFirstDelete { get; set; }
        public bool CrossSeasonFile { get; set; }
        public bool ImportOnGrab { get; set; }
        public bool UnknownHistory { get; set; }
        public int FirstFileId { get; set; } = 9;
        public int QueuedEpisode { get; set; }

        public Fixture()
        {
            Context = new ReleaseContext(Guid.NewGuid(), JObject.Parse("{id:42,type:'tv',is4k:false,status:2,requestedBy:{id:8},"
                + "media:{tmdbId:123,tvdbId:456},seasons:[{seasonNumber:1,status:2}]}"),
                JObject.Parse("{id:0,hostname:'localhost',port:8989,apiKey:'secret'}"), false, CanChooseEpisodes: false);
            Service = new ServarrReleaseService(this, NullLogger<ServarrReleaseService>.Instance);
        }

        public HttpClient CreateClient(string name) => new(this, false);
        public Task<JObject> Search() => Service.SearchSeasonAsync(Context, 1, default);
        public ReleaseDownloadPayload Payload(JObject search, bool reject = false) => new()
            { SearchToken = search.Value<string>("searchToken")!, SeasonNumber = 1, ReleaseIndex = 0, RejectCurrent = reject };
        public Task<JObject> Download(JObject search, bool reject = false) => Service.DownloadAsync(Context, Payload(search, reject), default);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.PathAndQuery.Replace("/api/v3/", "");
            if (request.Method != HttpMethod.Get)
            {
                Mutations.Add(request.Method + " " + path);
                if (path == "release")
                {
                    Posted = JObject.Parse(await request.Content!.ReadAsStringAsync(ct));
                    if (FailGrab) return Json(new JObject(), HttpStatusCode.Conflict);
                    if (ImportOnGrab) FirstFileId = 99;
                }
                return Json(new JObject(), FailFirstDelete && path == "episodefile/9" ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
            }
            if (path == "series") return Json(JArray.Parse("[{id:5,title:'Series',tmdbId:123,tvdbId:456,qualityProfileId:1}]"));
            if (path == "episode?seriesId=5") return Json(new JArray(
                Episode(21, 1, 1, FirstFileId), Episode(22, 1, 2, FirstFileId), Episode(23, 1, 3, 10), Episode(24, 2, 1, CrossSeasonFile ? 9 : 20)));
            if (path.StartsWith("episodefile/"))
            {
                int id = int.Parse(path.Split('/')[1]);
                return Json(new JObject { ["id"] = id, ["path"] = $"/library/{id}.mkv", ["sceneName"] = UnknownHistory ? "" : "Old.Season.Pack" });
            }
            if (path == "history/series?seriesId=5&seasonNumber=1")
                return Json(UnknownHistory ? new JArray() : JArray.Parse("[{id:100,eventType:'grabbed',sourceTitle:'Old.Season.Pack',downloadId:'old'},"
                    + "{id:101,eventType:'downloadFolderImported',downloadId:'old',data:{importedPath:'/library/9.mkv'}},"
                    + "{id:102,eventType:'downloadFolderImported',downloadId:'old',data:{importedPath:'/library/10.mkv'}}]"));
            if (path == "release?seriesId=5&seasonNumber=1")
            {
                JObject release = JObject.Parse("{guid:'new',indexerId:3,title:'New.Season.Pack',approved:true,rejected:false,rejections:[],"
                    + "fullSeason:true,mappedSeriesId:5,seasonNumbers:[1],mappedSeasonNumber:1,mappedEpisodeNumbers:[1,2,3],quality:{quality:{resolution:1080}}}");
                if (Incomplete) release["mappedEpisodeNumbers"] = new JArray(1, 2);
                if (ScopeProblem == "multi-season") release["seasonNumbers"] = new JArray(1, 2);
                if (ScopeProblem == "wrong-series") release["mappedSeriesId"] = 99;
                if (ScopeProblem == "unknown-coverage") release["mappedEpisodeNumbers"] = new JArray();
                if (ScopeProblem == "single-episode") release["fullSeason"] = false;
                if (ScopeProblem == "mapped-other-season") release["mappedEpisodeInfo"] = JArray.Parse("[{seasonNumber:2,episodeNumber:1}]");
                if (Rejections.Count > 0) { release["rejected"] = true; release["approved"] = false; release["rejections"] = Rejections.DeepClone(); }
                return Json(new JArray(release));
            }
            if (path.StartsWith("queue?")) return Json(new JObject { ["records"] = QueuedEpisode > 0
                ? new JArray(new JObject { ["episodeId"] = QueuedEpisode, ["seriesId"] = 5 }) : new JArray() });
            throw new InvalidOperationException("Unexpected API call: " + path);
        }

        private static JObject Episode(int id, int season, int number, int file) => new()
            { ["id"] = id, ["seriesId"] = 5, ["seasonNumber"] = season, ["episodeNumber"] = number, ["episodeFileId"] = file, ["title"] = $"Episode {number}" };
        private static HttpResponseMessage Json(JToken body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
            { Content = new StringContent(body.ToString(), Encoding.UTF8, "application/json") };
    }
}
