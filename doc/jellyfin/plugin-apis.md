# Jellyfin plugin HTTP APIs

Base route: **`/JellySeerr`** (`[Route("JellySeerr")]`).  
Unless noted, endpoints require Jellyfin `[Authorize]` (logged-in session).

Target: Jellyfin **10.11.x**, `targetAbi` `10.11.0.0`, NuGet **10.11.2**, `net9.0`.

Deep companions: [inject-and-client.md](inject-and-client.md) · [networking.md](networking.md) · [../permissions.md](../permissions.md).

## Embedded assets (any authenticated user)

| Method | Path | Content-Type |
|--------|------|--------------|
| GET | `jellyseerr-tabs.js` | application/javascript |
| GET | `jellyseerr-tabs.css` | text/css |
| GET | `jellyseerr-nativeui.js` | application/javascript |
| GET | `jellyseerr-modal.js` | application/javascript |
| GET | `jellyseerr-modal.css` | text/css |
| GET | `jellyseerr-requests.js` | application/javascript |
| GET | `jellyseerr-requests.css` | text/css |

Injected into `index.html` via File Transformation plugin (relative URLs `../JellySeerr/...`).

## Admin only (`Administrator` role)

| Method | Path | Returns |
|--------|------|---------|
| GET | `Configuration` | Full `PluginConfiguration` |
| GET | `admin/health` | Connection summary |
| POST | `admin/test` | Seerr + Arr status chips payload |
| POST | `admin/import-seerrfin` | Import URL/keys from SeerrFin XML |
| POST | `admin/sync-quality` | Synced `QualityProfileEntry` list |
| GET | `admin/users` | Jellyfin↔Seerr mapping table |

## Session / display / client

| Method | Path | Consumer | Notable JSON |
|--------|------|----------|--------------|
| GET | `session` | tabs | `mapped`, `hideUnmapped`, `canManage`, `disable4k`, … |
| GET | `display-settings` | tabs | tab visibility, poster/backdrop prefs, `hideUnmapped` |
| GET | `client-settings` | modal + requests | `tmdbApiKey`, `isAdmin`, `canManageRequests`, `canOpenLocalServices`, Arr/Seerr public URLs when allowed |
| GET | `quota` | modal | Proxied Seerr user quota |
| GET | `request-options/{mediaType}` | quality picker | `canRequest`, `options[]`, defaults |

## Discover / search / media

| Method | Path | Upstream |
|--------|------|----------|
| GET | `discover/movies/trending` … | Seerr discover (+ optional TMDB) |
| GET | `discover/tv/...` including `anime` | Seerr |
| GET | `discover/movies/genre/{id}` etc. | Seerr |
| GET | `discover/*/studio\|network\|provider/{id}` | Seerr |
| GET | `search` | Seerr search |
| GET | `genres/movie`, `genres/tv` | Seerr genreslider |
| GET | `providers/movie`, `providers/tv` | Seerr watchproviders |
| GET | `studios/movie`, `networks/tv` | curated + images |
| GET | `details/{mediaType}/{mediaId}` | Seerr movie/tv + profile annotate |
| GET | `library-item/{mediaType}/{tmdbId}` | Jellyfin library TMDB match |
| GET | `justwatch/qualities/{mediaType}/{tmdbId}` | JustWatch via TMDB metadata |
| GET | `backdrop/{mediaType}/{tmdbId}` | TMDB images helper |
| GET | `CachedImage/{cacheKey}` | local image cache |
| POST | `backdrops` | batch backdrop resolve |

## Requests / Seerr actions

| Method | Path | Upstream |
|--------|------|----------|
| GET | `requests` | Seerr request list + Arr enrich |
| POST | `request` | Seerr POST request |
| PUT | `request/{id}` | Seerr PUT request |
| DELETE | `request/{id}` | Seerr DELETE (+ optional unmonitor) |
| POST | `request/{id}/approve\|decline\|retry` | Seerr manage |
| POST | `requests/bulk-cancel` | loop DELETE |
| POST | `watchlist` | Seerr watchlist |
| DELETE | `watchlist/{tmdbId}` | Seerr watchlist |
| POST | `issue` | Seerr issue |
| POST | `servarr/unmonitor` | Radarr/Sonarr PUT |
| GET | `proxy/avatar` | Seerr avatar bytes |
| GET/POST | `seerr/{*path}` | raw Seerr proxy with `X-Api-User` |

## In-process (no HTTP)

| API | Usage |
|-----|--------|
| `ILibraryManager.GetItemsResult` | Exact TMDB / TheMovieDb provider id match for Play |
| `IUserManager` | Session user, admin checks |
| Plugin configuration + disk cache | Keys, quality catalog, images |
| File Transformation (reflection) | Script injection |

## Library Play match rules

`FindLibraryItemId` / `HasTmdbProviderId`:

- Item type Movie vs Series from `mediaType`.
- Provider id keys: `Tmdb`, `TheMovieDb` (and casing variants).
- Must equal the requested TMDB id — never “first movie in library”.
