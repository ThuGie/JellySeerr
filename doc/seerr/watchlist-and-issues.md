# Seerr watchlist and issues

Code: [`RequestService.WatchlistAsync` / `CreateIssueAsync`](../../src/Jellyfin.Plugin.JellySeerr/Services/RequestService.cs), modal buttons in [`jellyseerr-modal.js`](../../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-modal.js).

## Watchlist — POST `/api/v1/watchlist`

Zod schema (`watchlistCreate`):

| Field | Required | Notes |
|-------|----------|-------|
| `tmdbId` | yes | number |
| `mediaType` | yes | `movie` \| `tv` |
| `title` | no | display string |
| `ratingKey` | no | Plex; unused by us |

### Example

```json
{
  "tmdbId": 550,
  "mediaType": "movie",
  "title": "Fight Club"
}
```

Plugin always sends as the mapped user (`X-Api-User`). Success **201**; duplicate **409**.

### UI detection

Modal treats title as watchlisted when `mediaInfo.watchlists` / `watchLists` (or root) contains entries for the current user when present.

## Watchlist — DELETE `/api/v1/watchlist/{tmdbId}`

**Query required:** `mediaType=movie|tv`.

```http
DELETE /api/v1/watchlist/550?mediaType=movie
X-Api-Key: …
X-Api-User: 12
```

Success **204**. Invalid mediaType → **400**.

Plugin route: `DELETE /JellySeerr/watchlist/{tmdbId}?mediaType=`.

## Issues — POST `/api/v1/issue`

| Field | Required | Notes |
|-------|----------|-------|
| `issueType` | yes | number (UI currently sends `1`) |
| `message` | yes | string |
| `mediaId` | yes | **Seerr Media.id**, not TMDB |
| `userId` | no | optional |

### Example

```json
{
  "issueType": 1,
  "message": "Wrong audio track",
  "mediaId": 42
}
```

### Critical field map

| Client field | Meaning |
|--------------|---------|
| Modal `seerrMediaId` | `mediaInfo.id` (fallback wrongly to tmdb only if id missing) |
| Plugin `IssuePayload.MediaId` | Forwarded as `mediaId` |

Issue types on Seerr include video/audio/subtitles/other (see OpenAPI issue count breakdown). We do not yet expose a type picker beyond default `1`.

Success **201**.
