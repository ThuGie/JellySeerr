# User tools (1.0.21.0)

Restart Jellyfin after installing the update, then reload the browser.

## Release calendar (updated in 1.0.23.0)

Click the **calendar icon in Jellyfin's top header**, the **Release calendar** home tab, or **Requests → Release calendar**. They open the same dedicated page. It shows all monitored series/episodes from connected Sonarr instances, regardless of who requested them. Switch between a month grid and an agenda, filter by series/episode, and optionally hide episodes already available. Phones start in agenda view.

Air times come from Sonarr and are shown in your browser's local time zone. Continuing/upcoming monitored series with no known next air date appear under **Awaiting an air date**. Ended series and series with monitoring disabled do not appear in that list. Air dates are estimates, not promises of download availability. See the [calendar guide](monitored-calendar.md) for scope and permissions.

The page reads Sonarr's calendar directly; there is no request-count limit or scan of every requested season. Results are cached for one minute, concurrent identical lookups are shared, and instance failures are shown without hiding successful results. Nothing loads until the calendar page is opened. The older personal Seerr/TMDB calendar endpoint remains available for compatibility.

## Notification preferences

Open **Requests → Notifications**. Select events for email, Discord, Telegram, browser push, Pushbullet or Pushover, then save. Preferences are stored in Seerr under the mapped user's account. Each channel requires server configuration and, where applicable, a linked account. Email changes, channel credentials and browser push registration remain in Seerr.

The browser receives only channel availability and event masks. Credentials stay server-side and are preserved when Seerr's endpoint replaces other settings fields. Unrelated channels and notification bits are retained. Users cannot select another account or modify global notification agents.

## Report and follow issues

Use **Report issue** in title details. Choose Video/playback, Audio, Subtitles or Wrong content/other, describe the problem, and optionally specify a season/episode. Existing visible reports and their open/resolved state appear below; add comments to an existing report to avoid duplicates. Requests and comments use the mapped Seerr account and its issue permissions.

Messages and comments are limited to 4000 characters. The latest 1000 visible reports are checked for this title; the interface states when that limit is reached. Users do not gain moderation permissions.

## Download and replacement history

Use **Replacement history** in approved/completed title details. It combines recent Sonarr/Radarr events with plugin actions, outcomes, timestamps, seasons/episodes, and the Jellyfin username performing a plugin action. Older events can appear from service history without an invented user attribution.

Plugin activity is persisted atomically at `DataPath/jellyseerr/release-activity.json`, retaining the last 2000 actions across the server. The response includes up to 100 plugin actions and recent native events for the selected request. History is scoped to the request, media and server. It stores release titles, not download URLs, API keys or library paths. Disk errors are logged without undoing an accepted download.

## Compare releases

After searching in **Advanced download**, select **Compare** beside a result. The table shows current inspected media information and candidate quality/size. Current season files may have different formats. Candidate codec/HDR values inferred from the release name are labeled as inference; unknown languages, subtitles or bit depth remain unknown. Comparison cannot guarantee player compatibility.

## Recover a failed or stalled download

Open **Recover download** in approved/completed title details. Only failed, stalled, error or warning queue entries with an established scope are offered. The dialog lists affected episodes. Confirm **Remove, blocklist & search again** to remove that download and its partial data, blocklist its specific release, and start a targeted movie, episode or season search.

The indexer remains enabled. Existing library files are retained. Another release is not guaranteed to exist. If removal succeeds but the search fails, the result says so and suggests a manual search or Advanced download. Native blocklisting follows the release identity tracked by Sonarr/Radarr; a renamed/reposted release may be considered a different source.

Recovery uses short-lived, user-bound, single-use selections and rechecks fresh state before removal. Healthy downloads, active imports, unresolved scopes and downloads spanning unapproved seasons/titles are excluded. Requesters/managers can recover their episode downloads. Another user's TV request permits whole-season recovery only, covering the complete approved season; confirmation explains the shared impact. Existing request permissions apply without requiring an administrator role.

## Validation

Backend tests cover notification credential preservation, invalid preferences, calendar boundaries, history persistence/scoping, recovery ownership and replay protection, changed healthy state, shared download exclusion, whole-season permissions, and search failure after removal. Interface tests cover calendar filtering/navigation, notifications, issues/comments, recovery confirmation, safe text rendering, history, comparison and closing during loading.

Tests use simulated service responses. A live Seerr/Sonarr/Radarr/download-client deployment is still needed to verify provider-specific behavior and actual notification delivery.

Upstream references: [Seerr settings](https://github.com/seerr-team/seerr/blob/develop/server/routes/user/usersettings.ts), [Seerr issues](https://github.com/seerr-team/seerr/blob/develop/server/routes/issue.ts), [Seerr movie metadata](https://github.com/seerr-team/seerr/blob/develop/server/models/Movie.ts), [Sonarr queue](https://github.com/Sonarr/Sonarr/blob/develop/src/Sonarr.Api.V3/Queue/QueueController.cs), [Radarr queue](https://github.com/Radarr/Radarr/blob/develop/src/Radarr.Api.V3/Queue/QueueController.cs).
