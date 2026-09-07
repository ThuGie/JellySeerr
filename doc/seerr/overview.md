# Seerr API overview

Config keys in the plugin still say `JellyseerrUrl` / `JellyseerrApiKey`; product copy says **Seerr**. The HTTP surface is Seerr/Jellyseerr-compatible **`/api/v1`**.

## Base URL and client

`{JellyseerrUrl}` with paths under `/api/v1/...`.  
[`SeerrApiClient`](../../src/Jellyfin.Plugin.JellySeerr/Services/SeerrApiClient.cs):

- Prefixes `/api/v1/` when the relative path does not already include it.
- Timeout **30 seconds**.
- Returns `(statusCode, body, contentType)` for mutating calls; `GetJsonAsync` for JSON GETs.

## Authentication

| Header | Purpose |
|--------|---------|
| `X-Api-Key` | Admin API key from Seerr Settings → General. Documented. |
| `X-Api-User` | Numeric Seerr user id. Impersonates that user for permission/quota. **Not** in OpenAPI security schemes; established behavior with the admin key. |

Cookie auth (`connect.sid`) is unused by this plugin.

Deep dive: [auth-and-users.md](auth-and-users.md).

## Deep pages in this folder

| Doc | Topic |
|-----|--------|
| [endpoints.md](endpoints.md) | Path index |
| [auth-and-users.md](auth-and-users.md) | Mapping, quota, headers |
| [discover-and-search.md](discover-and-search.md) | Browse/search/details |
| [requests.md](requests.md) | Create/update/cancel/manage |
| [watchlist-and-issues.md](watchlist-and-issues.md) | Watchlist + issue `mediaId` |
| [services-and-quality.md](services-and-quality.md) | Radarr/Sonarr via Seerr |
| [media-status.md](media-status.md) | Enums (6=BLOCKLISTED, 7=DELETED) |

## Status enums (source of truth)

From Seerr `server/constants/media.ts`:

### MediaRequestStatus

| Value | Name |
|------:|------|
| 1 | PENDING |
| 2 | APPROVED |
| 3 | DECLINED |
| 4 | FAILED |
| 5 | COMPLETED |

### MediaStatus

| Value | Name |
|------:|------|
| 1 | UNKNOWN |
| 2 | PENDING |
| 3 | PROCESSING |
| 4 | PARTIALLY_AVAILABLE |
| 5 | AVAILABLE |
| 6 | BLOCKLISTED |
| 7 | DELETED |

> OpenAPI historically documented `6 = DELETED` and omitted `BLOCKLISTED`. **Trust the TypeScript enum.**

## Client files

- `Services/SeerrApiClient.cs` — shared HttpClient factory + send/get
- `Services/ConnectionService.cs` — `/status`, `/auth/me`
- `Services/UserMappingService.cs` — `/user/jellyfin/{guid}`, `/user?q=`
- `Services/DiscoveryService.cs` — search, discover, movie/tv details
- `Services/RequestService.cs` — request CRUD, watchlist, issues
- `Services/RequestListService.cs` — request list + avatar proxy
- `Services/QualityCatalogService.cs` — `/service/{radarr\|sonarr}`
- `Controllers/JellySeerrController.cs` — `seerr/{*path}` catch-all proxy
