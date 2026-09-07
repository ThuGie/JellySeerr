# Seerr endpoints we call

Auth on all routes: `X-Api-Key`; user-scoped routes also send `X-Api-User` when a Seerr user is resolved.

## Status / auth / users

| Method | Path | Body / query | Fields we use |
|--------|------|--------------|---------------|
| GET | `/api/v1/status` | — | `version` |
| GET | `/api/v1/auth/me` | — | `displayName` |
| GET | `/api/v1/user/jellyfin/{jellyfinUserId}` | GUID `D` format | `id`, `permissions`, `displayName`/`username`, `email` |
| GET | `/api/v1/user?q={username}` | — | `results[]` with `jellyfinUsername`, `jellyfinUserId`, `id`, `permissions` |
| GET | `/api/v1/user/{id}/quota` | — | proxied for UI |

## Search / discover / details

| Method | Path | Notes |
|--------|------|-------|
| GET | `/api/v1/search?query=&language=` | Results may use TMDB-style snake_case or camelCase |
| GET | `/api/v1/discover/trending` | `page` |
| GET | `/api/v1/discover/movies` | `page`, optional `sortBy`, `genre` |
| GET | `/api/v1/discover/movies/upcoming` | `page` |
| GET | `/api/v1/discover/movies/studio/{id}` | `page` |
| GET | `/api/v1/discover/movies/language/en` | `watchRegion`, `watchProvider`, `page` |
| GET | `/api/v1/discover/tv` | same + anime via `genre=16&keywords=210024` |
| GET | `/api/v1/discover/tv/upcoming` | `page` |
| GET | `/api/v1/discover/tv/network/{id}` | `page` |
| GET | `/api/v1/discover/tv/language/en` | providers |
| GET | `/api/v1/discover/genreslider/movie` | bare array or `{ results }` |
| GET | `/api/v1/discover/genreslider/tv` | same |
| GET | `/api/v1/watchproviders/movies?watchRegion=` | same shape |
| GET | `/api/v1/watchproviders/tv?watchRegion=` | same |
| GET | `/api/v1/movie/{tmdbId}` | details + `mediaInfo` / nested requests |
| GET | `/api/v1/tv/{tmdbId}` | details + seasons + `mediaInfo` |

## Requests

### GET `/api/v1/request`

Query: `take`, `skip`, `sort=added`, `sortDirection=desc`, optional `filter`, `requestedBy`.

Filters we map: `pending`, `available`, `processing`, `failed`, `unavailable`, `completed`. UI “coming soon” uses Seerr `processing` then client-side date filtering.

Parsed: `results[]`, `pageInfo.pages`; per request `id`, `type`, `status`, `is4k`, `createdAt`, `serverId`, `profileId`, `rootFolder`, `seasons`, `media`, `requestedBy`.

### POST `/api/v1/request`

Required: `mediaType` (`movie`|`tv`), `mediaId` (TMDB id).  
Optional: `seasons` (`number[]` or `"all"`), `is4k`, `serverId`, `profileId`, `rootFolder`, `languageProfileId`, `tags`, `userId`, `ignoreQuota`.

Notable responses: **201** created, **409** duplicate, **202** no seasons available, **403** permission/quota/blocklist.

### GET `/api/v1/request/{id}`

Used before PUT to backfill `mediaType` / seasons.

### PUT `/api/v1/request/{id}`

**Required:** `mediaType`.  
Optional: `seasons` (required non-empty for TV updates in route logic), `serverId`, `profileId`, `rootFolder`, `languageProfileId`, `userId`, `is4k`.  
Do **not** send `mediaId` on update (create-only).

### DELETE `/api/v1/request/{id}`

Cancel / remove request.

### POST `/api/v1/request/{id}/approve|decline|retry`

Approve/decline match OpenAPI `/request/{requestId}/{status}` with `approve`|`decline`. Retry is a dedicated path. Bodies: `{}` where applicable.

## Watchlist / issues

| Method | Path | Body / query |
|--------|------|--------------|
| POST | `/api/v1/watchlist` | `tmdbId`, `mediaType`, optional `title` / `ratingKey` |
| DELETE | `/api/v1/watchlist/{tmdbId}?mediaType=` | `mediaType` required (`movie`\|`tv`) |
| POST | `/api/v1/issue` | `issueType`, `message`, `mediaId` (**Seerr Media.id**, not TMDB) |

## Services (quality catalog)

| Method | Path | Fields |
|--------|------|--------|
| GET | `/api/v1/service/radarr` | server list (`id`, `name`, `is4k`) |
| GET | `/api/v1/service/sonarr` | same |
| GET | `/api/v1/settings/radarr` | fallback if service list empty |
| GET | `/api/v1/settings/sonarr` | fallback |
| GET | `/api/v1/service/radarr/{id}` | `server`, `profiles[]`, `rootFolders[]`, `tags` |
| GET | `/api/v1/service/sonarr/{id}` | same + `isAnime`, language profiles |

## Avatars

GET paths under `/avatar`, `/avatarproxy`, `/api/v1/avatar` — proxied by plugin; path traversal blocked.

## Catch-all

Plugin routes `GET|POST JellySeerr/seerr/{*path}` forward to Seerr with the caller's mapped `X-Api-User`.
