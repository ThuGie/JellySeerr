# Sonarr TMDB lookup

## The problem

Sonarr’s canonical id is **TVDB**. Documented list filters use `tvdbId`. Adding series via API also requires TVDB.

Seerr requests are keyed by **TMDB**. Modern Sonarr series resources include a `tmdbId` field when SkyHook knows the mapping.

## What we do today

`FindByTmdbAsync(client, "series", tmdbId)`:

```text
1. GET /api/v3/series?tmdbId={id}
2. Pick first item where item.tmdbId == id
3. If none: GET /api/v3/series/lookup?term=tmdb:{id}
4. Read tvdbId from the matching lookup row
5. GET /api/v3/series?tvdbId={tvdbId}  (documented filter → in-library only)
6. If still none: GET /api/v3/series (full library) and scan for tmdbId
```

Lookup alone is **not** used as the unmonitor target — SkyHook results may not be in the library. Steps 5–6 only accept library series.

### Risk

`series?tmdbId=` is **not** a guaranteed documented filter (unlike Radarr’s `movie?tmdbId=`). Sonarr may ignore the query and return the full list, or return an unexpected subset. The TVDB path (steps 3–5) avoids a full download when lookup + `tvdbId` succeed. Step 6 remains the last resort.

Progress enrichment usually avoids per-title lookup: it loads the full series list once per snapshot and builds a `Dictionary<tmdbId, series>`.

## External service id shortcut

If Seerr media has `externalServiceId`, we `GET /api/v3/series/{id}` directly and skip TMDB search for that title.

## Seerr parity

Seerr itself uses `term=tmdb:{id}` when resolving missing TVDB ids during request processing.

## Audit status

Documented as **Risk** in [AUDIT.md](../AUDIT.md). Hardening (**implemented**): lookup → `tvdbId` before full-list fallback.
