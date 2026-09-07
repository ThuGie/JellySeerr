# Upstream documentation sources

Checked while building and deepening this doc pack (**2026-09-07**). Prefer the **source of truth** column when OpenAPI and code disagree.

## Seerr

| Resource | URL | Notes |
|----------|-----|--------|
| API docs hub | https://docs.seerr.dev/api/seerr-api/ | Auth overview (`X-Api-Key`, cookie) |
| OpenAPI | https://github.com/seerr-team/seerr/blob/develop/seerr-api.yml | Endpoint schemas; **media status enum can lag** |
| Media enums | https://github.com/seerr-team/seerr/blob/develop/server/constants/media.ts | **Source of truth** for status ints |
| Request routes | https://github.com/seerr-team/seerr/blob/develop/server/routes/request.ts | PUT requires `mediaType`; TV seasons |
| Request body type | https://github.com/seerr-team/seerr/blob/develop/server/interfaces/api/requestInterfaces.ts | `MediaRequestBody` |
| Watchlist routes | https://github.com/seerr-team/seerr/blob/develop/server/routes/watchlist.ts | POST create / DELETE by tmdbId |
| Watchlist zod | https://github.com/seerr-team/seerr/blob/develop/server/interfaces/api/watchlistCreate.ts | `tmdbId`, `mediaType`, optional `title` |
| Jellyseerr fork enums | https://github.com/fallenbagel/jellyseerr/blob/develop/server/constants/media.ts | Same MediaStatus / MediaRequestStatus values |

Deep docs: [seerr/](seerr/).

## Radarr

| Resource | URL | Notes |
|----------|-----|--------|
| Movie API | Radarr wiki / develop autodocs | `GET/PUT /api/v3/movie`, `?tmdbId=` |
| System status | `/api/v3/system/status` | `X-Api-Key` header |
| Queue | `/api/v3/queue` | `page`, `pageSize`, `includeMovie`, `totalRecords` |
| Python client mirror | https://github.com/devopsarr/radarr-py | Generated OpenAPI method list |

Deep docs: [radarr/](radarr/).

## Sonarr

| Resource | URL | Notes |
|----------|-----|--------|
| Series / episode autodocs | https://github.com/Sonarr/Sonarr (v5-develop `_autodocs`) | `GET /api/v3/series`, `GET /api/v3/episode?seriesId=` |
| Lookup | `GET /api/v3/series/lookup?term=` | `term=tmdb:{id}` for TMDB→TVDB; **not** `series?tmdbId=` as a guaranteed filter |
| Queue | `/api/v3/queue` | `includeSeries`, `includeEpisode` |

Deep docs: [sonarr/](sonarr/) especially [sonarr/tmdb-lookup.md](sonarr/tmdb-lookup.md).

## TMDB

| Resource | URL | Notes |
|----------|-----|--------|
| Developer portal | https://developer.themoviedb.org/ | |
| Auth | https://developer.themoviedb.org/docs/authentication-application | Bearer read-access token preferred; v3 `api_key` still valid |
| append_to_response | https://developer.themoviedb.org/docs/append-to-response | Browser details modal |
| Image CDN | https://developer.themoviedb.org/docs/image-basics | `https://image.tmdb.org/t/p/{size}{path}` |

Deep docs: [tmdb/](tmdb/).

## JustWatch

| Resource | URL | Notes |
|----------|-----|--------|
| GraphQL | `https://apis.justwatch.com/graphql` | **No public official API docs**; shapes reverse-engineered |
| Our usage | [justwatch/graphql.md](justwatch/graphql.md) | Full query text + variables |

## Jellyfin

| Resource | URL | Notes |
|----------|-----|--------|
| Plugin ABI | Jellyfin 10.11.x (`targetAbi` 10.11.0.0) | NuGet packages 10.11.2 |
| Library queries | In-process `ILibraryManager` | TMDB provider id match for Play |

Deep docs: [jellyfin/](jellyfin/).

## Plugin code anchors

| Area | Path |
|------|------|
| Seerr HTTP | `src/Jellyfin.Plugin.JellySeerr/Services/SeerrApiClient.cs` |
| Requests | `Services/RequestService.cs` |
| Discover | `Services/DiscoveryService.cs` |
| Arr progress | `Services/ServarrProgressService.cs` |
| JustWatch | `Services/JustWatchQualitiesService.cs` |
| Routes | `Controllers/JellySeerrController.cs` |
| Inject UI | `Inject/jellyseerr-*.js` |
