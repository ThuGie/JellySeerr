# Sonarr API overview

Base: `{SonarrUrl}/api/v3/`  
Auth: `X-Api-Key: {SonarrApiKey}`.

## Deep pages

| Doc | Topic |
|-----|--------|
| [endpoints.md](endpoints.md) | Path index |
| [series-episode-queue.md](series-episode-queue.md) | Series, episodes, queue, progress |
| [unmonitor.md](unmonitor.md) | Season-aware PUT |
| [tmdb-lookup.md](tmdb-lookup.md) | TMDB vs TVDB matching risk |

## TMDB vs TVDB

Sonarr’s primary id is **TVDB**. Series resources include a `tmdbId` field when known. This plugin matches Seerr TV requests by that field (with full-list fallback). Details: [tmdb-lookup.md](tmdb-lookup.md).

## Client files

Same as Radarr: `ServarrProgressService`, `ConnectionService`.
