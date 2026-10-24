using System.Net;
using System.Text;
using Jellyfin.Plugin.JellySeerr.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Jellyfin.Plugin.JellySeerr.Tests;

public class UserToolsTests
{
    [Fact]
    public void Notification_view_hides_credentials_and_merge_preserves_them_and_unrelated_bits()
    {
        JObject original = JObject.Parse("{emailEnabled:true,telegramEnabled:true,telegramChatId:'secret-chat',pushbulletAccessToken:'secret-token',pgpKey:'secret-pgp',notificationTypes:{email:40,telegram:8,webhook:99}}");
        string view = UserFeaturePolicy.NotificationView(original).ToString();
        Assert.DoesNotContain("secret", view);
        JObject merged = UserFeaturePolicy.MergeNotifications(original, new() { NotificationTypes = new() { ["email"] = 4 } });
        Assert.Equal("secret-chat", merged.Value<string>("telegramChatId"));
        Assert.Equal("secret-token", merged.Value<string>("pushbulletAccessToken"));
        Assert.Equal(36, merged["notificationTypes"]!.Value<int>("email")); // preserve the test-notification bit
        Assert.Equal(99, merged["notificationTypes"]!.Value<int>("webhook"));
        Assert.Equal(40, original["notificationTypes"]!.Value<int>("email"));
    }

    [Fact]
    public void Notification_unknown_channel_or_unlinked_account_is_rejected()
    {
        Assert.Throws<ReleaseSelectionException>(() => UserFeaturePolicy.MergeNotifications(new JObject(), new() { NotificationTypes = new() { ["admin"] = 8 } }));
        Assert.Throws<ReleaseSelectionException>(() => UserFeaturePolicy.MergeNotifications(JObject.Parse("{telegramEnabled:true}"), new() { NotificationTypes = new() { ["telegram"] = 8 } }));
        Assert.Throws<ReleaseSelectionException>(() => UserFeaturePolicy.MergeNotifications(new JObject(), new() { NotificationTypes = new() { ["email"] = int.MaxValue } }));
    }

    [Fact]
    public void Calendar_excludes_end_boundary_and_unknown_dates_and_preserves_calendar_day()
    {
        JObject req = JObject.Parse("{id:42,status:2,media:{status:3}}");
        JObject? Item(string? date) => UserFeaturePolicy.CalendarEvent(date, new DateTime(2026, 10, 1), new DateTime(2026, 11, 1), "Title", "tv", 123, "Episode", req);
        Assert.Null(Item(null)); Assert.Null(Item("invalid")); Assert.Null(Item("2026-11-01")); Assert.Null(Item("2026-09-30"));
        Assert.Equal("2026-10-31", Item("2026-10-31")!.Value<string>("date"));
        Assert.Equal(42, Item("2026-10-01")!.Value<int>("requestId"));
        Assert.Equal("2026-10-01", Item("2026-10-01T00:00:00Z")!.Value<string>("date"));
    }

    [Fact]
    public void Activity_survives_restart_is_scoped_and_does_not_expose_actor_ids()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "history.json");
        try
        {
            var context = Context() with { ActorName = "Alice" };
            var store = new ReleaseActivityStore(path, NullLogger<ReleaseActivityStore>.Instance);
            store.Add(context, "Replacement accepted", "Queued", "Release", 1, 2);
            var restarted = new ReleaseActivityStore(path, NullLogger<ReleaseActivityStore>.Instance);
            Assert.Empty(restarted.Read(context with { Request = JObject.Parse("{id:43}") }, context.UserId));
            Assert.Equal("You", Assert.Single(restarted.Read(context, context.UserId)).Value<string>("actor"));
            Assert.Empty(restarted.Read(context with { Server = JObject.Parse("{hostname:'different-server'}") }, context.UserId));
            string other = restarted.Read(context, Guid.NewGuid()).ToString();
            Assert.Contains("Alice", other); Assert.DoesNotContain(context.UserId.ToString(), other);
            Assert.DoesNotContain("secret", other);
        }
        finally { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(Path.GetDirectoryName(path))) Directory.Delete(Path.GetDirectoryName(path)!); }
    }

    [Fact]
    public async Task Recovery_is_user_bound_single_use_and_blocklists_only_selected_download()
    {
        using var f = new RecoveryFixture();
        JObject options = await f.Service.RecoveryOptionsAsync(f.Context, default);
        string token = ((JArray)options["downloads"]!)[0]!.Value<string>("token")!;
        Assert.DoesNotContain("secret", options.ToString()); Assert.DoesNotContain("download-a", options.ToString());
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.RecoverAsync(f.Context with { UserId = Guid.NewGuid() }, token, default));
        Assert.Empty(f.Mutations);
        var result = await f.Service.RecoverAsync(f.Context, token, default);
        Assert.Contains("specific release", result.Value<string>("message"));
        Assert.Equal(new[] { "DELETE queue/10?removeFromClient=true&blocklist=true&skipRedownload=true&changeCategory=false", "POST command" }, f.Mutations);
        Assert.Equal("MoviesSearch", f.Command!.Value<string>("name"));
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.RecoverAsync(f.Context, token, default));
        Assert.Equal(2, f.Mutations.Count);
    }

    [Fact]
    public async Task Recovery_refuses_a_now_healthy_download()
    {
        using var f = new RecoveryFixture();
        var options = await f.Service.RecoveryOptionsAsync(f.Context, default);
        f.State = "ok";
        await Assert.ThrowsAsync<ReleaseSelectionException>(() => f.Service.RecoverAsync(f.Context, options["downloads"]![0]!.Value<string>("token")!, default));
        Assert.Empty(f.Mutations);
        Assert.Empty((JArray)(await f.Service.RecoveryOptionsAsync(f.Context, default))["downloads"]!);
    }

    [Fact]
    public async Task Recovery_reports_search_failure_after_successful_removal()
    {
        using var f = new RecoveryFixture { FailCommand = true };
        var options = await f.Service.RecoveryOptionsAsync(f.Context, default);
        var result = await f.Service.RecoverAsync(f.Context, options["downloads"]![0]!.Value<string>("token")!, default);
        Assert.Contains("new search could not be confirmed", result.Value<string>("message"));
        Assert.Equal(2, f.Mutations.Count);
    }

    [Fact]
    public async Task Recovery_never_removes_download_shared_with_another_movie()
    {
        using var f = new RecoveryFixture { Shared = true };
        Assert.Empty((JArray)(await f.Service.RecoveryOptionsAsync(f.Context, default))["downloads"]!);
        Assert.Empty(f.Mutations);
    }

    [Fact]
    public async Task Shared_TV_recovery_requires_the_whole_approved_season()
    {
        using var f = new RecoveryFixture { Tv = true };
        var context = f.Context with { Request = JObject.Parse("{id:42,type:'tv',status:2,media:{tmdbId:123,tvdbId:456},seasons:[{seasonNumber:1,status:2}]}"), CanChooseEpisodes = false };
        Assert.Empty((JArray)(await f.Service.RecoveryOptionsAsync(context, default))["downloads"]!);
        f.Pack = true;
        var options = await f.Service.RecoveryOptionsAsync(context, default);
        Assert.Equal(1, options["downloads"]![0]!.Value<int>("seasonNumber"));
        await f.Service.RecoverAsync(context, options["downloads"]![0]!.Value<string>("token")!, default);
        Assert.Equal("SeasonSearch", f.Command!.Value<string>("name"));
        Assert.Equal(1, f.Command.Value<int>("seasonNumber"));
    }

    private static ReleaseContext Context() => new(Guid.NewGuid(), JObject.Parse("{id:42,type:'movie',status:2,media:{tmdbId:123}}"),
        JObject.Parse("{id:0,hostname:'localhost',port:7878,apiKey:'secret'}"), false);

    private sealed class RecoveryFixture : HttpMessageHandler, IHttpClientFactory
    {
        public ReleaseContext Context { get; } = UserToolsTests.Context();
        public ServarrReleaseService Service { get; }
        public string State { get; set; } = "warning";
        public bool Shared { get; set; }
        public bool FailCommand { get; set; }
        public bool Tv { get; set; }
        public bool Pack { get; set; }
        public List<string> Mutations { get; } = [];
        public JObject? Command { get; private set; }
        public RecoveryFixture() { Service = new(this, NullLogger<ServarrReleaseService>.Instance); }
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string route = request.RequestUri!.PathAndQuery.Split("/api/v3/")[1];
            string json;
            if (request.Method != HttpMethod.Get)
            {
                Mutations.Add(request.Method + " " + route);
                if (route == "command")
                {
                    Command = JObject.Parse(await request.Content!.ReadAsStringAsync(ct));
                    if (FailCommand) return new HttpResponseMessage(HttpStatusCode.BadGateway);
                }
                json = "{}";
            }
            else if (route.StartsWith("movie?")) json = "[{id:5,tmdbId:123}]";
            else if (route == "series") json = "[{id:5,tvdbId:456}]";
            else if (route.StartsWith("episode?")) json = "[{id:21,seriesId:5,seasonNumber:1,episodeNumber:1},{id:22,seriesId:5,seasonNumber:1,episodeNumber:2}]";
            else if (route.StartsWith("queue?"))
            {
                JObject q = new() { ["id"] = 10, [Tv ? "seriesId" : "movieId"] = 5, ["downloadId"] = "download-a", ["title"] = "Failed.Release", ["status"] = "downloading", ["trackedDownloadStatus"] = State };
                if (Tv) { q["seasonNumber"] = 1; if (!Pack) q["episodeId"] = 21; }
                JArray rows = new(q);
                if (Shared) rows.Add(JObject.Parse("{id:11,movieId:6,downloadId:'download-a',status:'failed'}"));
                json = new JObject { ["records"] = rows, ["totalRecords"] = rows.Count }.ToString();
            }
            else throw new Exception("Unexpected " + route);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
