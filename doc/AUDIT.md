# API usage audit

Audit date: **2026-09-07**  
Plugin version at audit: **1.0.9.0**  
Method: static compare of code paths vs official Seerr/Servarr/TMDB docs (and reverse-engineered JustWatch). No live E2E against a Seerr stack.

**Deep documentation:** the advanced reference tree (examples, field maps, sequences) lives beside this file — start at [README.md](README.md). Architecture and permissions: [architecture.md](architecture.md), [permissions.md](permissions.md).

Severity:

- **Broken** — wrong vs current upstream source of truth; user-visible bug
- **Risk** — works in common cases but fragile / undocumented
- **OK** — matches docs / intentional
- **Fixed** — was Broken; corrected in the same pass as this doc pack

---

## Summary

| Area | Result |
|------|--------|
| Seerr request create/update/list | OK (PUT `mediaType` fixed in 1.0.8.0; empty TV seasons rejected) |
| Seerr MediaStatus 6/7 | **Broken → Fixed** (BLOCKLISTED/DELETED were swapped) |
| Seerr watchlist / issue | **Fixed** (issue `mediaId` is Seerr Media.id only; no TMDB fallback) |
| Seerr services / quality sync | **Fixed** (sync saves config first; test `ok` includes Arr) |
| Radarr movie/queue/unmonitor | OK |
| Sonarr series/episode/queue | OK with TMDB lookup **Risk** |
| TMDB auth + endpoints | OK |
| JustWatch GraphQL | Risk (unofficial) |
| Jellyfin library Play match | **Fixed** (query both `Tmdb` and `TheMovieDb`) |
| HideRequested / Letterboxd stubs | **Fixed** (ignore cancel stubs) |
| Requests `openUrl` gate | **Fixed** (admin ∧ LAN, same as client-settings) |
| Requests user mapping | **Fixed** (GUID via `UserMappingService`, not username-only) |

---

## Seerr

| Check | Severity | Notes |
|-------|----------|-------|
| Auth `X-Api-Key` | OK | Matches docs |
| `X-Api-User` impersonation | Risk | Not in OpenAPI security schemes; required for per-user quota/permissions; widely used by Seerr clients — see [seerr/auth-and-users.md](seerr/auth-and-users.md) |
| POST `/request` body (`mediaType`, `mediaId`, seasons `"all"`) | OK | Matches `MediaRequestBody` — [seerr/requests.md](seerr/requests.md) |
| PUT `/request/{id}` requires `mediaType` | OK | Fixed in 1.0.8.0; TV seasons backfilled from existing request |
| GET request filters / sort | OK | Matches OpenAPI enums |
| Approve / decline / retry paths | OK | `/request/{id}/approve\|decline\|retry` |
| Watchlist POST (`tmdbId`, `mediaType`, `title`) | OK | Matches zod `watchlistCreate` |
| Watchlist DELETE `?mediaType=` | OK | Required by route |
| Issue POST (`issueType`, `message`, `mediaId`) | **Fixed** | `mediaId` is Seerr Media.id; modal only shows Report when `mediaInfo.id` exists |
| Service radarr/sonarr detail | OK | Profiles / rootFolders / tags |
| **MediaStatus enum** | **Fixed** | Seerr source: `6=BLOCKLISTED`, `7=DELETED`. Plugin had them reversed. OpenAPI YAML still says `6=DELETED` — **ignore YAML**, trust `server/constants/media.ts`. Details: [seerr/media-status.md](seerr/media-status.md) |
| MediaRequestStatus 1–5 | OK | Includes FAILED=4, COMPLETED=5 |
| Cancel leaves empty `mediaInfo` stub | OK | Documented quirk; UI + HideRequested/Letterboxd treat as re-requestable when no live request |
| Season picker blocklisted | **Fixed** | Status `6` seasons are not requestable |
| PUT TV empty `seasons` | **Fixed** | Returns 400 instead of forwarding `[]` to Seerr |
| Catch-all `seerr/{*path}` | Risk | Powerful proxy; gated by Jellyfin session + mapped user — [jellyfin/networking.md](jellyfin/networking.md) |

### MediaStatus fix detail

Before: string/name maps and labels treated `6=Deleted`, `7=Blocklisted`.  
After: `6=Blocklisted`, `7=Deleted`, matching Seerr/Jellyseerr enums.

---

## Radarr

| Check | Severity | Notes |
|-------|----------|-------|
| `X-Api-Key` header | OK | |
| `GET system/status` | OK | |
| `GET movie` / `movie?tmdbId=` / `movie/{id}` | OK | [radarr/movie-and-queue.md](radarr/movie-and-queue.md) |
| Queue pagination `page`/`pageSize`/`totalRecords` | OK | |
| `includeMovie=true` | OK | |
| PUT full movie for unmonitor | OK | [radarr/unmonitor.md](radarr/unmonitor.md) |
| Quality nesting `quality.quality.resolution` | OK | |

---

## Sonarr

| Check | Severity | Notes |
|-------|----------|-------|
| `GET series` / `series/{id}` | OK | |
| `GET series?tmdbId=` | Risk | Not a documented filter — mitigated by lookup→`tvdbId` — [sonarr/tmdb-lookup.md](sonarr/tmdb-lookup.md) |
| `GET episode?seriesId=&includeEpisodeFile=true` | OK | Path is `/episode` (singular) |
| Queue includes | OK | |
| PUT series unmonitor | OK | [sonarr/unmonitor.md](sonarr/unmonitor.md) |
| Prefer lookup `term=tmdb:` for single-title fetch | **Done** | `FindSeriesViaTmdbLookupAsync` before full-list fallback |

---

## TMDB

| Check | Severity | Notes |
|-------|----------|-------|
| Bearer vs `api_key` detection | OK | [tmdb/auth.md](tmdb/auth.md) |
| discover / trending / images / release_dates | OK | |
| Browser `append_to_response` | OK | |
| Key exposed to browser via client-settings | Risk | [tmdb/browser-vs-server.md](tmdb/browser-vs-server.md) |

---

## JustWatch

| Check | Severity | Notes |
|-------|----------|-------|
| GraphQL URL + UA | Risk | [justwatch/graphql.md](justwatch/graphql.md) |
| `presentationType` map | OK | Skips `CANVAS` |
| Title match via TMDB name+year | Risk | Ambiguous titles possible |

---

## Jellyfin

| Check | Severity | Notes |
|-------|----------|-------|
| ABI 10.11 / net9 | OK | |
| Library Play by exact TMDB provider id | **Fixed** | Query + match `Tmdb` and `TheMovieDb` |
| LAN gate for *arr/Seerr URLs | **Fixed** | Requests list uses admin ∧ LAN (matches client-settings) |
| Requests user resolve | **Fixed** | Uses `UserMappingService.ResolveAsync` (GUID then username) |
| Test connection overall `ok` | **Fixed** | Requires configured Arr endpoints to pass when present |
| Sync quality profiles | **Fixed** | Saves plugin config before sync (same as test) |

---

## Not broken (verified)

- Request quality change sending `mediaType` (1.0.8.0)
- Quality profile resolution labels (SD/720p/1080p/2K/4K) are UI helpers, not upstream API contracts
- Anime discover via Seerr filters (`genre=16&keywords=210024`) is intentional Seerr behavior

---

## Follow-ups

1. ~~Prefer Sonarr `series/lookup?term=tmdb:{id}` before full library scan~~ **Done** — lookup → `series?tvdbId=` → full scan.
2. ~~Unit test MediaStatus name↔int maps vs Seerr `media.ts`~~ **Done** — `MediaStatusHelper` + `MediaStatusHelperTests`.
3. ~~Do not reintroduce `6=DELETED` from stale OpenAPI~~ **Done** — helper comments, docs, and tests assert OpenAPI lag; re-check `seerr-api.yml` when bumping Seerr assumptions (still wrong as of 2026-09-07).

Optional later: prefer Sonarr lookup only when `externalServiceId` is missing (already skipped when present).
