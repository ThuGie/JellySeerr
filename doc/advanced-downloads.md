# Advanced downloads

Open a title from Movies, TV or Requests, then choose **Advanced download**. Select your approved/completed request, select an episode for TV, and press **Search releases**. Filter by release name, quality or indexer. **Show unavailable releases** reveals rejected results and their reasons.

Normal requesters can select releases for their own requests. Managers can select releases for other requesters. This does not require Seerr's advanced-request permission or Jellyfin deletion permission. New titles must first go through the normal request/approval flow; this picker does not bypass approval or quotas and does not stop an automatic search already started by Seerr.

If that movie/episode already has a download in the Arr queue, the picker asks you to wait or resolve that download first rather than queueing another copy.

For an existing movie/episode, **Replace with this** confirms the operation. The optional **Mark the current release as bad** checkbox uses the original grabbed history event to blocklist that specific release in Sonarr/Radarr. It does not block the indexer. The selected download is queued first; only after acceptance does the plugin mark the old release and remove its imported library file. The title may be unavailable until the new download imports. If queuing fails, the old file remains. If later cleanup fails, the UI says the replacement was queued and Jellyfin logs the cleanup failure; check Arr activity before trying again.

The original release must be identifiable from import history or the file's scene name. If history has been purged, replacement is still offered but blocklisting is disabled. Native blocklisting cannot guarantee that a re-upload under another identity contains different bytes. Sonarr/Radarr's own failed-download handling remains active.

Quality, language, seeders, blocklist and other profile restrictions remain enforced. Only rejection reasons concerning the existing file's upgrade/cutoff status are relaxed for replacement. Standard requests cannot select 4K releases. TV searches cover only approved seasons on the selected request. This version supports single-episode releases; season packs, multi-episode releases and shared episode files must be managed in Sonarr.

## Connections

The backend resolves the actual Radarr/Sonarr instance from Seerr, including separate 4K/anime instances. Credentials and release download URLs never go to the browser. Jellyfin must be able to reach the instance's address. If Seerr uses an internal Docker hostname, configure the plugin's Radarr/Sonarr URL and the same API key to use a Jellyfin-reachable address for that instance.

## API

- `GET /JellySeerr/request/{id}/downloads`: title and permitted episode options.
- `GET /JellySeerr/request/{id}/releases?episodeId=...`: sanitized interactive search results and an opaque search token.
- `POST /JellySeerr/request/{id}/releases`: `{ "searchToken": "...", "releaseIndex": 0, "rejectCurrent": true }`.

Each operation reloads request ownership, status and permission checks. Search tokens are bound to the user, request, media and server, expire after ten minutes and are consumed before a grab. The backend refreshes release eligibility before queuing and checks for changed files/profiles. Raw release URLs, arbitrary Arr IDs and override flags are not accepted from the client.

## Validation

`dotnet test JellySeerr.sln --configuration Release` exercises ownership/status policy, rejection rules, 4K restrictions, history correlation, replacement order, failed grabs, token replay, changed files, cleanup failure and episode boundaries using simulated Arr API responses.

`npm ci --prefix tests/ui` followed by `npm test --prefix tests/ui` exercises the picker with a simulated DOM and API. CI and release workflows run both suites before packaging the plugin. These tests do not replace a live smoke test with the installed Seerr/Arr versions.
