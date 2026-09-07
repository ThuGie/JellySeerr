# Sonarr series, episodes, and queue (deep)

Code: [`ServarrProgressService.cs`](../../src/Jellyfin.Plugin.JellySeerr/Services/ServarrProgressService.cs).

## GET `/api/v3/system/status`

Same role as Radarr for health chips.

## GET `/api/v3/series`

Full library cache `servarr:sonarr:series`.

### Fields

| Field | Use |
|-------|-----|
| `id` | Episode/queue joins |
| `tmdbId` | Match Seerr TV requests |
| `monitored` | Series-level monitoring |
| `titleSlug` | Open URL `/series/{titleSlug}` |
| `statistics.sizeOnDisk` | Size fallback |
| `seasons[].seasonNumber` | Filter requested seasons |
| `seasons[].monitored` | Unmonitor / progress |
| `seasons[].statistics.episodeFileCount` | Partial vs complete |
| `seasons[].statistics.episodeCount` | Denominator |
| `seasons[].statistics.sizeOnDisk` | Bytes |

## Episodes

`GET /api/v3/episode?seriesId={id}&includeEpisodeFile=true`

(Path is **`episode`**, singular — Servarr convention.)

| Field | Use |
|-------|-----|
| `seasonNumber` | Filter to requested seasons |
| `monitored` / `hasFile` | Library progress |
| `airDate` / `airDateUtc` | Unreleased episodes |
| `episodeFile.quality` | Resolution (same nesting as Radarr) |
| `episodeFile.size` / `sizeOnDisk` | Bytes |

Cached per series: `servarr:sonarr:episodes:{seriesId}`.

## Queue

`GET /api/v3/queue?page=&pageSize=250&includeSeries=true&includeEpisode=true`

Same pagination pattern as Radarr. Join on `seriesId`; season filter uses nested `episode.seasonNumber`.

## Progress build order (TV)

1. Queue hits for series (+ season filter) → Queued.
2. Season statistics path (`BuildFromSeriesSeasons`) if it already shows files.
3. Else episode list: partial (`N of M episodes`), full downloaded, missing, unreleased.
4. Attach highest / mixed quality from episode files or queue.

Specials (season `0`) included only when advanced setting `IncludeSpecialsInSeriesProgress` is true.

## Related

- [tmdb-lookup.md](tmdb-lookup.md) — how we find series by TMDB
- [unmonitor.md](unmonitor.md)
