using Jellyfin.Plugin.JellySeerr.Model;
using Jellyfin.Plugin.JellySeerr.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Controllers;

public partial class JellySeerrController
{
    [HttpPost("request/{id:int}/release-jobs")]
    [Authorize]
    public Task<IActionResult> StartReleaseJob(int id, [FromServices] IUserManager users,
        [FromServices] ReleaseOperationJobs jobs, [FromServices] ServarrReleaseService releases,
        [FromBody] ReleaseJobPayload payload, CancellationToken ct) =>
        WithReleaseContext(id, users, context =>
        {
            if (payload.Download != null)
            {
                if (string.IsNullOrWhiteSpace(payload.Download.SearchToken) || payload.Download.SearchToken.Length > 64)
                    throw new ReleaseSelectionException(400, "Search before downloading.");
                return Task.FromResult(jobs.Start(context, "download:" + payload.Download.SearchToken,
                    token => releases.DownloadAsync(context, payload.Download, token)));
            }
            if (string.IsNullOrWhiteSpace(payload.ClientToken) || payload.ClientToken.Length > 64
                || payload.EpisodeId < 0 || payload.SeasonNumber < 0 || payload.SeasonNumber.HasValue && payload.EpisodeId != 0)
                throw new ReleaseSelectionException(400, "Choose an episode or a season and start a new search.");
            return Task.FromResult(jobs.Start(context, "search:" + payload.ClientToken,
                token => payload.SeasonNumber.HasValue ? releases.SearchSeasonAsync(context, payload.SeasonNumber.Value, token)
                    : releases.SearchAsync(context, payload.EpisodeId, token)));
        }, ct, sharedSeason: payload.Download?.SeasonNumber.HasValue == true || payload.SeasonNumber.HasValue);

    [HttpGet("request/{id:int}/release-jobs/{token}")]
    [Authorize]
    public Task<IActionResult> ReleaseJobStatus(int id, string token, [FromServices] IUserManager users,
        [FromServices] ReleaseOperationJobs jobs, CancellationToken ct) =>
        WithOwnSeerrUser(users, _ => Task.FromResult(jobs.Get(GetUserId(), id, token)), ct);
}

public sealed class ReleaseJobPayload
{
    public string ClientToken { get; set; } = "";
    public int EpisodeId { get; set; }
    public int? SeasonNumber { get; set; }
    public ReleaseDownloadPayload? Download { get; set; }
}
