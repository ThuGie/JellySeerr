# Availability and missing episodes

Open a title and choose **Episodes & status** (TV) or **Download status** (movies). The panel loads on demand; it does not add background polling to discovery pages.

Select a request and, for TV, a season. Each episode shows its title, availability, an explanation and its air date when known. Available episodes link to **Choose replacement**; eligible missing episodes link to **Choose release**, opening Advanced download on that exact episode.

States include available, missing, downloading, paused, stalled (when reported by the client), waiting for import, import blocked, download needs attention, not aired, unknown air date, monitoring off and search requested recently. Movie status also explains waiting for release. Pending, declined and failed Seerr requests have explanations without requiring the title to exist in Arr yet. A missing file with no queue entry does not prove that no suitable release exists; interactive search shows actual candidates and rejection reasons.

Availability is reported by Sonarr/Radarr. It is not a playback validation, and a file reported by Arr may still need to be scanned by Jellyfin. Raw error messages, private filesystem paths, download URLs and credentials are not returned to users.

## Find missing episodes

For your own approved/completed TV request, select the missing episode checkboxes and choose **Find missing episodes**. Managers can also search for other requesters. Up to 100 episodes can be selected per search. Confirming submits Sonarr's automatic `EpisodeSearch` command for eligible episode IDs only.

Before dispatch, the backend reloads the season and the complete download queue. It excludes:

- Episodes already associated with a file.
- Episodes that have not aired, or whose air date is unknown.
- Episodes or series with monitoring off.
- Episodes already covered by an active episode or season download.
- Episodes outside the approved season on the selected request.

The action does not delete files, change monitoring, switch profiles or blocklist releases. Sonarr applies its configured release rules; a search does not guarantee a download. An episode can change while a command is being processed, and Sonarr's normal upgrade rules still apply. The plugin records a two-minute cooldown per server/series/season before dispatch, including uncertain command responses, to reduce repeated searches. Refresh the panel to see downloads enter the queue.

Other users' TV requests are viewable, and whole-season replacement remains available as before. Viewing a shared request does not grant permission to search or replace its individual episodes. Other users' movie requests remain restricted to managers.

## Endpoints

- `GET /JellySeerr/request/{id}/availability`: sanitized request/movie/episode states.
- `POST /JellySeerr/request/{id}/search-missing`: `{ "seasonNumber": 1, "episodeIds": [123, 124] }`. Omitting `episodeIds` searches all eligible missing episodes in that approved season, subject to the 100-episode limit.

Both require a mapped Jellyfin/Seerr user and the existing request/4K permissions. The write endpoint uses the normal approved-request ownership checks. The browser's selected IDs and cached view are never treated as proof that an episode is still missing.
