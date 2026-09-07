# JellySeerr integration docs (advanced)

Curated **developer reference** for every external service this plugin talks to: contracts, examples, field maps, errors, and call flows. These are not full upstream manuals — they document what **we use**, with links to official sources.

## Reading order

1. [architecture.md](architecture.md) — layers and sequence diagrams  
2. [permissions.md](permissions.md) — Seerr bits + UI/LAN gates  
3. [AUDIT.md](AUDIT.md) — correctness vs upstream (Broken / Risk / OK)  
4. Service folders below for endpoint-level detail  
5. [sources.md](sources.md) — upstream URLs used for the audit  

## Contents

### Cross-cutting

| Path | What it covers |
|------|----------------|
| [architecture.md](architecture.md) | Browser → plugin → Seerr/Arr/TMDB/JustWatch |
| [permissions.md](permissions.md) | Permission bits, owner checks, manager tools |
| [AUDIT.md](AUDIT.md) | Audit findings + MediaStatus fix |
| [sources.md](sources.md) | Official doc links |

### Seerr (`/api/v1`)

| Path | Topic |
|------|--------|
| [seerr/overview.md](seerr/overview.md) | Auth headers, enums, file map |
| [seerr/endpoints.md](seerr/endpoints.md) | Path index |
| [seerr/auth-and-users.md](seerr/auth-and-users.md) | Mapping, quota, `X-Api-User` |
| [seerr/discover-and-search.md](seerr/discover-and-search.md) | Browse, search, details, caching |
| [seerr/requests.md](seerr/requests.md) | Create / PUT / cancel / manage |
| [seerr/watchlist-and-issues.md](seerr/watchlist-and-issues.md) | Watchlist + issue `mediaId` |
| [seerr/services-and-quality.md](seerr/services-and-quality.md) | Quality catalog via Seerr services |
| [seerr/media-status.md](seerr/media-status.md) | Status enums (`6=BLOCKLISTED`, `7=DELETED`) |

### Radarr / Sonarr (`/api/v3`)

| Path | Topic |
|------|--------|
| [radarr/overview.md](radarr/overview.md) | Auth + index |
| [radarr/endpoints.md](radarr/endpoints.md) | Path table |
| [radarr/movie-and-queue.md](radarr/movie-and-queue.md) | Library, queue, quality, progress |
| [radarr/unmonitor.md](radarr/unmonitor.md) | Full-body PUT |
| [sonarr/overview.md](sonarr/overview.md) | Auth + index |
| [sonarr/endpoints.md](sonarr/endpoints.md) | Path table |
| [sonarr/series-episode-queue.md](sonarr/series-episode-queue.md) | Series/episodes/queue |
| [sonarr/unmonitor.md](sonarr/unmonitor.md) | Season-aware unmonitor |
| [sonarr/tmdb-lookup.md](sonarr/tmdb-lookup.md) | TMDB vs TVDB risk |

### TMDB / JustWatch

| Path | Topic |
|------|--------|
| [tmdb/overview.md](tmdb/overview.md) | Index |
| [tmdb/auth.md](tmdb/auth.md) | Bearer vs `api_key` |
| [tmdb/browser-vs-server.md](tmdb/browser-vs-server.md) | Who calls TMDB |
| [tmdb/endpoints.md](tmdb/endpoints.md) | Paths + `append_to_response` |
| [justwatch/overview.md](justwatch/overview.md) | Unofficial GraphQL pipeline |
| [justwatch/graphql.md](justwatch/graphql.md) | Full queries, variables, offer map |

### Jellyfin plugin surface

| Path | Topic |
|------|--------|
| [jellyfin/plugin-apis.md](jellyfin/plugin-apis.md) | Every `/JellySeerr` route |
| [jellyfin/inject-and-client.md](jellyfin/inject-and-client.md) | Inject JS → routes |
| [jellyfin/networking.md](jellyfin/networking.md) | LAN gate, image cache, proxies |

## How to re-audit

1. Refresh [sources.md](sources.md) (Seerr OpenAPI + `server/constants/media.ts`, Servarr docs, TMDB docs).
2. Diff deep pages against code under `src/Jellyfin.Plugin.JellySeerr/Services/` and `Inject/`.
3. Prefer **Seerr TypeScript enums/routes** over OpenAPI YAML when they disagree (especially MediaStatus 6/7).
4. Update [AUDIT.md](AUDIT.md); fix anything marked Broken.

## Architecture sketch

```text
Jellyfin web UI (Inject JS)
  └─ ApiClient → /JellySeerr/*  (plugin, session auth)
        ├─ Seerr /api/v1/*     (X-Api-Key + X-Api-User)
        ├─ Radarr /api/v3/*    (X-Api-Key)
        ├─ Sonarr /api/v3/*    (X-Api-Key)
        ├─ TMDB api.themoviedb.org/3  (server or browser)
        └─ JustWatch GraphQL

Browser may also call TMDB directly when client-settings returns tmdbApiKey.
Seerr API key never leaves the server (except via authenticated seerr/{*path} proxy).
```
