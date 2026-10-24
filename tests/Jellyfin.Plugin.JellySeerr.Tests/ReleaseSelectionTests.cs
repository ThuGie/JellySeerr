using System.Net;
using System.Text;
using Jellyfin.Plugin.JellySeerr.Model;
using Jellyfin.Plugin.JellySeerr.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Jellyfin.Plugin.JellySeerr.Tests;

public class ReleaseSelectionTests
{
    [Theory]
    [InlineData(1, 7, false, false)]
    [InlineData(3, 7, true, false)]
    [InlineData(4, 7, true, false)]
    [InlineData(2, 7, false, true)]
    [InlineData(5, 7, false, true)]
    [InlineData(2, 8, false, false)]
    [InlineData(2, 8, true, true)]
    public void Approved_request_owner_can_choose_without_admin(int status, int user, bool manager, bool expected)
    {
        JObject request = JObject.Parse("{status:2,requestedBy:{id:7}}");
        request["status"] = status;
        Assert.Equal(expected, ReleaseSelectionPolicy.CanAccess(request, user, manager));
    }

    [Theory]
    [InlineData("Existing file meets cutoff: WEBDL-1080p", true, true)]
    [InlineData("Existing file meets cutoff: WEBDL-1080p", false, false)]
    [InlineData("Quality is not wanted in profile", true, false)]
    [InlineData("Release is blocklisted", true, false)]
    [InlineData("Not enough seeders", true, false)]
    [InlineData("Unknown future rejection", true, false)]
    public void Replacement_only_relaxes_existing_file_rejections(string reason, bool replacing, bool expected)
    {
        JObject release = Release();
        release["approved"] = false;
        release["rejected"] = true;
        release["rejections"] = new JArray(reason);
        Assert.Equal(expected, ReleaseSelectionPolicy.CanSelect(release, replacing, false));
        ((JArray)release["rejections"]!).Add("Quality is not wanted in profile");
        Assert.False(ReleaseSelectionPolicy.CanSelect(release, replacing, true));
    }

    [Fact]
    public void Standard_request_cannot_grab_4k()
    {
        JObject release = Release();
        release["quality"]!["quality"]!["resolution"] = 2160;
        Assert.False(ReleaseSelectionPolicy.CanSelect(release, true, false));
        Assert.True(ReleaseSelectionPolicy.CanSelect(release, true, true));
    }

    [Fact]
    public void Original_grab_is_matched_to_imported_path_not_latest_download()
    {
        JObject file = JObject.Parse("{id:9,path:'/library/movie.mkv',sceneName:'Old.Release'}");
        JArray history = JArray.Parse("[{id:12,eventType:'grabbed',sourceTitle:'New.Release',downloadId:'new'},"
            + "{id:11,eventType:'downloadFolderImported',downloadId:'old',data:{importedPath:'/library/movie.mkv'}},"
            + "{id:10,eventType:'grabbed',sourceTitle:'Old.Release',downloadId:'old'}]");
        Assert.Equal(10, ReleaseSelectionPolicy.FindGrab(history, file)!.Value<int>("id"));
        Assert.Null(ReleaseSelectionPolicy.FindGrab(history, JObject.Parse("{path:'/different/file.mkv'}")));
    }

    [Fact]
    public async Task Replacement_queues_before_blocklisting_and_deleting_old_file()
    {
        using var fixture = new Fixture();
        JObject search = await fixture.Search();
        Assert.DoesNotContain("apiKey", search.ToString());
        Assert.DoesNotContain("downloadUrl", search.ToString());
        JObject result = await fixture.Download(search, reject: true);
        Assert.Contains("sent to the download client", result.Value<string>("message"));
        Assert.Equal(new[] { "POST release", "POST history/failed/10", "DELETE moviefile/9" }, fixture.Mutations);
        Assert.Equal("next", fixture.PostedRelease!.Value<string>("guid"));
        Assert.Null(fixture.PostedRelease["downloadUrl"]);
        Assert.Null(fixture.PostedRelease["shouldOverride"]);
    }

    [Fact]
    public async Task Failed_grab_keeps_current_file_and_does_not_blocklist()
    {
        using var fixture = new Fixture { FailGrab = true };
        JObject search = await fixture.Search();
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Download(search, true));
        Assert.Equal(new[] { "POST release" }, fixture.Mutations);
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Download(search, true));
        Assert.Single(fixture.Mutations);
    }

    [Fact]
    public async Task Search_token_cannot_be_used_by_another_user_or_replayed()
    {
        using var fixture = new Fixture();
        JObject search = await fixture.Search();
        ReleaseContext other = fixture.Context with { UserId = Guid.NewGuid() };
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Service.DownloadAsync(other, fixture.Payload(search), default));
        Assert.Empty(fixture.Mutations);
        await fixture.Download(search);
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Download(search));
        Assert.Single(fixture.Mutations, x => x == "POST release");
    }

    [Fact]
    public async Task Current_file_changed_after_search_requires_new_search()
    {
        using var fixture = new Fixture();
        JObject search = await fixture.Search();
        fixture.FileId = 99;
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Download(search));
        Assert.Empty(fixture.Mutations);
    }

    [Fact]
    public async Task Fresh_search_rechecks_blocklist_before_grabbing()
    {
        using var fixture = new Fixture();
        JObject search = await fixture.Search();
        fixture.Rejection = "Release is blocklisted";
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Download(search));
        Assert.Empty(fixture.Mutations);
    }

    [Fact]
    public async Task Cleanup_failure_reports_queued_download_without_duplicate_grab()
    {
        using var fixture = new Fixture { FailDelete = true };
        JObject search = await fixture.Search();
        JObject result = await fixture.Download(search);
        Assert.Contains("Replacement queued", result.Value<string>("message"));
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Download(search));
        Assert.Single(fixture.Mutations, x => x == "POST release");
    }

    [Fact]
    public async Task Newly_imported_file_is_not_deleted_during_cleanup()
    {
        using var fixture = new Fixture { ChangeFileOnGrab = true };
        await fixture.Download(await fixture.Search());
        Assert.DoesNotContain(fixture.Mutations, x => x.StartsWith("DELETE"));
    }

    [Fact]
    public async Task Tv_search_is_scoped_to_request_seasons_and_rejects_packs()
    {
        using var fixture = new Fixture(tv: true);
        JObject options = await fixture.Service.OptionsAsync(fixture.Context, default);
        Assert.Single((JArray)options["episodes"]!);
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Service.SearchAsync(fixture.Context, 22, default));
        JObject search = await fixture.Search();
        Assert.True(search["releases"]![0]!.Value<bool>("selectable"));
        fixture.FullSeason = true;
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Download(search));
        Assert.Empty(fixture.Mutations);
    }

    [Fact]
    public async Task Shared_episode_file_cannot_be_deleted_by_single_episode_replacement()
    {
        using var fixture = new Fixture(tv: true) { SharedEpisodeFile = true };
        JObject search = await fixture.Search();
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Download(search));
        Assert.Empty(fixture.Mutations);
    }

    [Fact]
    public async Task Already_queued_download_does_not_create_a_duplicate()
    {
        using var fixture = new Fixture { ActiveDownload = true };
        JObject search = await fixture.Search();
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Download(search));
        Assert.Empty(fixture.Mutations);
    }

    [Fact]
    public async Task Missing_movie_can_be_downloaded_without_any_file_deletion()
    {
        using var fixture = new Fixture { FileId = 0 };
        JObject search = await fixture.Search();
        Assert.False(search.Value<bool>("hasFile"));
        await fixture.Download(search);
        Assert.Equal(new[] { "POST release" }, fixture.Mutations);
    }

    [Fact]
    public async Task Episode_replacement_only_deletes_selected_episode_file()
    {
        using var fixture = new Fixture(tv: true);
        await fixture.Download(await fixture.Search(), true);
        Assert.Equal(new[] { "POST release", "POST history/failed/10", "DELETE episodefile/9" }, fixture.Mutations);
        Assert.Equal(21, fixture.PostedRelease!.Value<int>("episodeId"));
    }

    private static JObject Release() => JObject.Parse("{guid:'next',indexerId:3,title:'Next.Release',approved:true,rejected:false,"
        + "rejections:[],quality:{quality:{name:'WEBDL-1080p',resolution:1080}},size:1000000,downloadUrl:'https://secret/?apikey=private',"
        + "seasonNumber:1,episodeNumbers:[1],fullSeason:false}");

    [Fact]
    public async Task Release_search_failure_identifies_upstream_http_status_without_exposing_credentials()
    {
        using var fixture = new Fixture { SearchFailure = HttpStatusCode.Unauthorized };
        var error = await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Search());
        Assert.Contains("release search returned HTTP 401", error.Message);
        Assert.Contains("API key", error.Message);
        Assert.DoesNotContain("secret", error.Message);
        Assert.Empty(fixture.Mutations);
    }

    [Fact]
    public async Task File_lookup_failure_identifies_the_failing_step()
    {
        using var fixture = new Fixture { FileLookupFailure = true };
        var error = await Assert.ThrowsAsync<ReleaseSelectionException>(() => fixture.Search());
        Assert.Contains("moviefile lookup returned HTTP 404", error.Message);
        Assert.Empty(fixture.Mutations);
    }

    private sealed class Fixture : HttpMessageHandler, IHttpClientFactory
    {
        public ServarrReleaseService Service { get; }
        public ReleaseContext Context { get; }
        public List<string> Mutations { get; } = [];
        public JObject? PostedRelease { get; private set; }
        public int FileId { get; set; } = 9;
        public bool FailGrab { get; set; }
        public bool FailDelete { get; set; }
        public bool ChangeFileOnGrab { get; set; }
        public bool FullSeason { get; set; }
        public bool SharedEpisodeFile { get; set; }
        public bool ActiveDownload { get; set; }
        public string? Rejection { get; set; }
        public HttpStatusCode? SearchFailure { get; set; }
        public bool FileLookupFailure { get; set; }
        private readonly bool _tv;

        public Fixture(bool tv = false)
        {
            _tv = tv;
            Context = new ReleaseContext(Guid.NewGuid(), JObject.Parse("{id:42,type:'" + (tv ? "tv" : "movie")
                + "',status:2,media:{tmdbId:123,tvdbId:456},seasons:[{seasonNumber:1,status:2}]}"),
                JObject.Parse("{id:0,hostname:'localhost',port:7878,baseUrl:'/arr',apiKey:'secret'}"), false);
            Service = new ServarrReleaseService(this, NullLogger<ServarrReleaseService>.Instance);
        }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        public Task<JObject> Search() => Service.SearchAsync(Context, _tv ? 21 : 0, default);
        public ReleaseDownloadPayload Payload(JObject search, bool reject = false) => new()
            { SearchToken = search.Value<string>("searchToken")!, ReleaseIndex = 0, RejectCurrent = reject };
        public Task<JObject> Download(JObject search, bool reject = false) => Service.DownloadAsync(Context, Payload(search, reject), default);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("secret", request.Headers.GetValues("X-Api-Key").Single());
            string path = request.RequestUri!.PathAndQuery.Replace("/arr/api/v3/", "");
            if (request.Method != HttpMethod.Get)
            {
                Mutations.Add(request.Method + " " + path);
                if (path == "release")
                {
                    PostedRelease = JObject.Parse(await request.Content!.ReadAsStringAsync(ct));
                    if (FailGrab) return Json("{}", HttpStatusCode.Conflict);
                    if (ChangeFileOnGrab) FileId = 99;
                }
                if (FailDelete && request.Method == HttpMethod.Delete) return Json("{}", HttpStatusCode.InternalServerError);
                return Json("{}");
            }
            JObject title = JObject.Parse("{id:5,title:'Movie',tmdbId:123,tvdbId:456,qualityProfileId:1}");
            title["movieFileId"] = FileId;
            JObject episode = JObject.Parse("{id:21,seriesId:5,seasonNumber:1,episodeNumber:1,title:'Episode'}");
            episode["episodeFileId"] = FileId;
            if (path.StartsWith("movie?") || path == "series") return Json(new JArray(title).ToString());
            if (path == "movie/5") return Json(title.ToString());
            if (path == "episode/21") return Json(episode.ToString());
            if (path.StartsWith("episode?"))
            {
                JObject outside = JObject.Parse("{id:22,seriesId:5,seasonNumber:2,episodeNumber:1,episodeFileId:0}");
                if (SharedEpisodeFile) outside["episodeFileId"] = FileId;
                return Json(new JArray(episode, outside).ToString());
            }
            if (path.StartsWith("moviefile/") || path.StartsWith("episodefile/"))
            {
                if (FileLookupFailure) return Json("{}", HttpStatusCode.NotFound);
                return Json(new JObject { ["id"] = FileId, ["path"] = "/library/file.mkv", ["sceneName"] = "Old.Release" }.ToString());
            }
            if (path.StartsWith("history"))
            {
                JArray history = JArray.Parse("[{id:10,eventType:'grabbed',sourceTitle:'Old.Release',downloadId:'old'},"
                    + "{id:11,eventType:'downloadFolderImported',downloadId:'old',data:{importedPath:'/library/file.mkv'}}]");
                return Json((_tv ? (JToken)new JObject { ["records"] = history } : history).ToString());
            }
            if (path.StartsWith("release?"))
            {
                if (SearchFailure.HasValue) return Json("{}", SearchFailure.Value);
                JObject release = Release();
                release["fullSeason"] = FullSeason;
                if (Rejection != null) { release["approved"] = false; release["rejected"] = true; release["rejections"] = new JArray(Rejection); }
                return Json(new JArray(release).ToString());
            }
            if (path.StartsWith("queue?"))
                return Json(new JObject { ["records"] = ActiveDownload ? new JArray(new JObject { ["movieId"] = 5, ["episodeId"] = 21 }) : new JArray() }.ToString());
            throw new InvalidOperationException("Unexpected API call: " + path);
        }

        private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
