# Radarr endpoints we call

Deep dives: [movie-and-queue.md](movie-and-queue.md) · [unmonitor.md](unmonitor.md).

| Method | Path | Query / body | Fields we use |
|--------|------|--------------|---------------|
| GET | `/api/v3/system/status` | — | `version`, `instanceName` |
| GET | `/api/v3/movie` | full library (cached) | `id`, `tmdbId`, `monitored`, `hasFile`, `status`, `sizeOnDisk`, `titleSlug`, `movieFile` |
| GET | `/api/v3/movie?tmdbId={id}` | documented filter | same; used by unmonitor lookup |
| GET | `/api/v3/movie/{id}` | by Radarr id / Seerr `externalServiceId` | same |
| GET | `/api/v3/queue` | `page`, `pageSize=250`, `includeMovie=true` | paginate while `page * pageSize < totalRecords`; `records[]` with `movieId`, `size`, `sizeleft`, `quality`, nested `movie` |
| PUT | `/api/v3/movie/{id}` | full movie JSON with `monitored: false` | unmonitor; leaves files on disk |

## Open URLs (browser only)

When LAN gate allows: `{RadarrUrl}/movie/{titleSlug}`.

## Notes

- Unmonitor sends the previously fetched movie resource with `monitored` flipped — required so Radarr receives a complete resource.
- Queue cache TTL is short (15s when Servarr caching is enabled).
- Movie `status` values like `announced` / `inCinemas` are treated as unreleased for progress UI.
