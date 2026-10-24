# Advanced downloads

Open a title from Movies, TV or Requests, then choose **Advanced download**. Select an approved/completed request and press **Search releases**. For TV, choose **Single episode** or **Replace whole season**, then select the episode or season. Filter by release name, quality or indexer. **Show unavailable releases** reveals rejected results and their reasons.

Normal requesters can select movies and individual episodes for their own requests. **Whole-season replacement is also available on other users' approved/completed TV requests**, without administrator approval. The user must be mapped to Seerr and have TV request permission (and 4K TV permission for a 4K request). Managers can select individual releases for other requesters too. This does not require Seerr's advanced-request permission or Jellyfin deletion permission. New titles must first go through the normal request/approval flow; this picker does not bypass approval or quotas and does not stop an automatic search already started by Seerr.

If that movie/episode already has a download in the Arr queue, the picker asks you to wait or resolve that download first rather than queueing another copy.

For an existing movie/episode, **Replace with this** confirms the operation. The optional **Mark the current release as bad** checkbox uses the original grabbed history event to blocklist that specific release in Sonarr/Radarr. It does not block the indexer. The selected download is queued first; only after acceptance does the plugin mark the old release and remove its imported library file. The title may be unavailable until the new download imports. If queuing fails, the old file remains. If later cleanup fails, the UI says the replacement was queued and Jellyfin logs the cleanup failure; check Arr activity before trying again.

The original release must be identifiable from import history or the file's scene name. If history has been purged, replacement is still offered but blocklisting is disabled. Native blocklisting cannot guarantee that a re-upload under another identity contains different bytes. Sonarr/Radarr's own failed-download handling remains active.

Quality, language, seeders, blocklist and other profile restrictions remain enforced. Only rejection reasons concerning the existing file's upgrade/cutoff status are relaxed for replacement. Standard requests cannot select 4K releases. TV searches cover only approved seasons on the selected request.

## Replacing a whole season

Choose **Replace whole season** to search Sonarr's season-pack results. You can select another user's request from the request list; individual-episode mode is disabled for those requests. The current-file panel lists the affected episodes and file count before the confirmation. Replacement affects the shared library for all viewers, regardless of who originally requested it.

Selectable packs must be mapped by Sonarr to the correct series, exactly one season and all known episodes in that season. Incomplete, multi-season and unverifiable packs are labeled and unavailable. This verifies Sonarr's metadata, not the actual contents of files before download. A season still airing may not have a selectable complete pack yet.

The selected pack is queued before any old file is removed. Old files shared by several episodes in the selected season are removed once; files shared with a different season are refused. Optional blocklisting marks each identified old download once, even if that pack supplied many episodes. All original releases must be identifiable to enable the blocklisting checkbox. Files, profile, pack eligibility and queue overlap are checked again before downloading, and newly imported replacement files are not deleted during cleanup. Single-episode mode still refuses multi-episode files.

## Connections

The backend resolves the actual Radarr/Sonarr instance from Seerr, including separate 4K/anime instances. Credentials and release download URLs never go to the browser. Jellyfin must be able to reach the instance's address. If Seerr uses an internal Docker hostname, configure the plugin's Radarr/Sonarr URL and the same API key to use a Jellyfin-reachable address for that instance.

## API

- `GET /JellySeerr/request/{id}/downloads`: title, episode and season options; `canChooseEpisodes` distinguishes ownership from shared season access.
- `GET /JellySeerr/request/{id}/releases?episodeId=...`: sanitized interactive search results and an opaque search token.
- `GET /JellySeerr/request/{id}/releases?seasonNumber=...`: full-season search, affected episodes and file count.
- `POST /JellySeerr/request/{id}/releases`: `{ "searchToken": "...", "releaseIndex": 0, "rejectCurrent": true }`; include `"seasonNumber": 1` for a season search. Season and episode tokens cannot be interchanged.

Each operation reloads request ownership, status and permission checks. Search tokens are bound to the user, request, media and server, expire after ten minutes and are consumed before a grab. The backend refreshes release eligibility before queuing and checks for changed files/profiles. Raw release URLs, arbitrary Arr IDs and override flags are not accepted from the client.

## Validation

`dotnet test JellySeerr.sln --configuration Release` exercises ownership/status policy, rejection rules, 4K restrictions, history correlation, replacement order, failed grabs, token replay, changed files, cleanup failure and episode boundaries using simulated Arr API responses.

`npm ci --prefix tests/ui` followed by `npm test --prefix tests/ui` exercises the picker with a simulated DOM and API. CI and release workflows run both suites before packaging the plugin. These tests do not replace a live smoke test with the installed Seerr/Arr versions.
