# Sonarr endpoints we call

Deep dives: [series-episode-queue.md](series-episode-queue.md) · [unmonitor.md](unmonitor.md) · [tmdb-lookup.md](tmdb-lookup.md).

| Method | Path | Query / body | Fields we use |
|--------|------|--------------|---------------|
| GET | `/api/v3/system/status` | — | `version`, `instanceName` |
| GET | `/api/v3/series` | full library | `id`, `tmdbId`, `monitored`, `titleSlug`, `statistics`, `seasons[]` |
| GET | `/api/v3/series?tmdbId={id}` | attempted lookup | may be ignored by Sonarr; fallback scans full list |
| GET | `/api/v3/series/{id}` | by Sonarr id / Seerr external id | same |
| GET | `/api/v3/episode` | `seriesId`, `includeEpisodeFile=true` | `seasonNumber`, `monitored`, `hasFile`, air dates, `episodeFile` |
| GET | `/api/v3/queue` | `page`, `pageSize=250`, `includeSeries=true`, `includeEpisode=true` | `seriesId`, sizes, `quality`, nested episode season |
| PUT | `/api/v3/series/{id}` | full series JSON; seasons `monitored: false` as needed | unmonitor |

## Open URLs

`{SonarrUrl}/series/{titleSlug}` when LAN gate allows.

## Notes

- Season 0 (specials) optional via advanced setting `IncludeSpecialsInSeriesProgress`.
- Unmonitor can clear series-level monitoring when no seasons remain monitored.
- If preferred *arr miss, unmonitor tries the other product (movie↔TV) as a fallback.