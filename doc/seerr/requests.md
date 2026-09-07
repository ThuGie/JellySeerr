# Seerr requests API (deep)

Code: [`RequestService.cs`](../../src/Jellyfin.Plugin.JellySeerr/Services/RequestService.cs), [`RequestListService.cs`](../../src/Jellyfin.Plugin.JellySeerr/Services/RequestListService.cs), [`jellyseerr-modal.js`](../../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-modal.js).

Upstream types: `MediaRequestBody` in Seerr `server/interfaces/api/requestInterfaces.ts`.

## POST `/api/v1/request` — create

### Contract

| Field | Required | Notes |
|-------|----------|-------|
| `mediaType` | yes | `movie` \| `tv` |
| `mediaId` | yes | **TMDB id** (not Seerr media row id) |
| `seasons` | TV | `number[]` or `"all"` |
| `is4k` | no | boolean |
| `serverId` | no | Seerr service server id |
| `profileId` | no | Radarr/Sonarr quality profile id |
| `rootFolder` | no | path string |
| `languageProfileId` | no | Sonarr |
| `tags` | no | int[] |
| `userId` | no | request as another user (manage) |
| `ignoreQuota` | no | manage only |

### Example (movie)

```http
POST /api/v1/request
X-Api-Key: …
X-Api-User: 12
Content-Type: application/json

{
  "mediaType": "movie",
  "mediaId": 550,
  "is4k": false,
  "serverId": 0,
  "profileId": 4,
  "rootFolder": "/movies"
}
```

### Example (TV seasons)

```json
{
  "mediaType": "tv",
  "mediaId": 1396,
  "seasons": [1, 2],
  "serverId": 0,
  "profileId": 6
}
```

Plugin may send `"seasons": "all"` on create when no explicit list and `includeMedia` path builds the body for new requests.

### Plugin pipeline

1. Resolve Seerr user; 400 if unmapped.
2. Reject 4K if `Disable4k`.
3. `HasRequestPermission(mediaType, is4k)`.
4. `ApplyProfileDefaults` if server/profile missing.
5. `QualityCatalogService.IsAllowed` if both ids present.
6. `BuildRequestBody(payload, includeMedia: true)` → POST.

### Status codes we surface

| Code | Meaning |
|-----:|---------|
| 201 | Created (modal treats as success when `id` present) |
| 202 | No seasons available / already covered (message only) |
| 403 | Permission, quota, blocklist, or profile disabled |
| 409 | Duplicate request |

Modal specially handles 202/message-without-id as failure-to-create, and 409 as “already requested” success path for movies.

---

## PUT `/api/v1/request/{id}` — update / change quality

### Contract

**Required:** `mediaType`.  
**Not used on update:** `mediaId` (create-only).  
**TV:** non-empty `seasons` array required by Seerr route logic.

Example:

```json
{
  "mediaType": "movie",
  "serverId": 0,
  "profileId": 7,
  "rootFolder": "/movies"
}
```

### Plugin pipeline (critical)

1. `GET /api/v1/request/{id}` as the user.
2. `ApplyExistingRequest` — fill `MediaType`, `MediaId`, and TV `Seasons` from existing if the client omitted them.
3. Build body with **`mediaType` always**, `mediaId` only when `includeMedia` (false for PUT).
4. PUT to Seerr.

Frontend sends both `MediaType`/`mediaType` and `MediaId`/`mediaId` for binder compatibility.

### Errors

Missing `mediaType` → Seerr validation: `request/body must have required property 'mediaType'` (fixed in plugin 1.0.8.0).  
Empty TV seasons → Seerr: “Missing seasons. If you want to cancel… use DELETE”.

---

## GET `/api/v1/request`

Query used by plugin:

| Param | Value |
|-------|-------|
| `take` | 1–200 (clamped) |
| `skip` | ≥ 0 |
| `sort` | `added` |
| `sortDirection` | `desc` |
| `filter` | see below |
| `requestedBy` | Seerr user id when not “all” |

### Filters

| UI / plugin filter | Seerr `filter` |
|--------------------|----------------|
| pending | pending |
| available | available |
| processing | processing |
| comingsoon | processing (+ client date filter) |
| failed | failed |
| unavailable | unavailable |
| completed | completed |

### Response shape (trimmed)

```json
{
  "pageInfo": { "pages": 3, "pageSize": 20, "results": 20, "page": 1 },
  "results": [
    {
      "id": 99,
      "status": 2,
      "type": "movie",
      "is4k": false,
      "createdAt": "2026-01-01T00:00:00.000Z",
      "serverId": 0,
      "profileId": 4,
      "rootFolder": "/movies",
      "media": {
        "tmdbId": 550,
        "mediaType": "movie",
        "status": 3,
        "status4k": 1,
        "externalServiceId": 12
      },
      "requestedBy": {
        "id": 12,
        "displayName": "Alice",
        "avatar": "/avatarproxy/…"
      },
      "seasons": [{ "seasonNumber": 1 }]
    }
  ]
}
```

### MapRequest enrichments

| Output field | Source |
|--------------|--------|
| `title`, `year`, posters | TMDB details fetch / cache |
| `mediaStatusLabel` | status enums — [media-status.md](media-status.md) |
| `profileName`, `qualityLabel` | quality catalog + `QualityLabelHelper` |
| `jellyfinItemId` | library TMDB match when playable |
| `servarrProgress` | Radarr/Sonarr enrichment |
| `seasonNumbers` | flattened from `seasons` |

---

## GET `/api/v1/request/{id}`

Used before PUT to backfill type/seasons. Parses `type`, `media.tmdbId`, `media.mediaType`, `seasons[]` as ints or `{ seasonNumber }`.

---

## DELETE `/api/v1/request/{id}`

Cancel. Plugin may unmonitor Arr **before** delete so ownership still holds. Seerr may leave empty `mediaInfo` stub afterward.

---

## POST `/api/v1/request/{id}/approve|decline|retry`

Bodies: `{}` where applicable. Requires manage permissions on Seerr side; UI gated by `canManageRequests`.

OpenAPI also describes `POST /request/{id}/{status}` with `approve`|`decline` — equivalent to our dedicated paths.

---

## Bulk cancel

Plugin `POST /JellySeerr/requests/bulk-cancel` loops DELETE per id with the user’s Seerr identity.
