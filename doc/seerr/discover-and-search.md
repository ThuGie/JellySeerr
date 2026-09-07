# Seerr discover and search

Code: [`DiscoveryService.cs`](../../src/Jellyfin.Plugin.JellySeerr/Services/DiscoveryService.cs), tabs UI in [`jellyseerr-tabs.js`](../../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-tabs.js).

All discover calls use `X-Api-Key` + `X-Api-User` when the Jellyfin user is mapped (so hide-available and language prefs match the user).

## Common result item shape

Seerr may return **camelCase** or **snake_case**. Discovery normalizes both.

| Concept | Possible fields |
|---------|-----------------|
| TMDB id | `id`, `tmdbId` |
| Title | `title`, `name`, `originalTitle`, `originalName` |
| Media kind | `mediaType` (`movie`/`tv`) |
| Dates | `releaseDate`, `firstAirDate` |
| Art | `posterPath` / `poster_path`, `backdropPath` / `backdrop_path` |
| Score | `voteAverage` / `vote_average` |
| Library state | `mediaInfo` (status, requests, …) |

Plugin maps into Jellyfin-like card DTOs with provider ids:

- `Tmdb` / `Jellyseerr`
- `JellyseerrMediaStatus` / `JellyseerrMediaStatus4k` (normalized ints as strings)

## Search

`GET /api/v1/search?query={q}&language={lang}`

| Response | Usage |
|----------|--------|
| `results[]` | Card list |
| `totalResults` / `total_results` / `total` | Paging |

Adult titles filtered when plugin settings demand it.

## Discover routes we call

| Plugin route idea | Seerr path | Extra query |
|-------------------|------------|-------------|
| Trending | `/discover/trending` | `page` |
| Popular movies | `/discover/movies` | `page`, `sortBy` |
| Top rated | `/discover/movies` | `sortBy=voteAverage.desc` (or similar) |
| Upcoming movies | `/discover/movies/upcoming` | `page` |
| Studio | `/discover/movies/studio/{id}` | `page` |
| Provider movies | `/discover/movies/language/en` | `watchRegion`, `watchProvider`, `page` |
| TV popular/top | `/discover/tv` | `sortBy`, `genre` |
| Upcoming TV | `/discover/tv/upcoming` | `page` |
| Network | `/discover/tv/network/{id}` | `page` |
| Provider TV | `/discover/tv/language/en` | region + provider |
| Anime | `/discover/tv` | **`genre=16&keywords=210024`** (Seerr default anime filters) |

### TMDB bypass for release types

When `DiscoverReleaseTypes` is configured for movies (non-upcoming), Discovery may call **TMDB** `discover/movie` / `trending` + `release_dates` instead of Seerr, then still attach Seerr `mediaInfo` when possible. See [tmdb/browser-vs-server.md](../tmdb/browser-vs-server.md).

## Genres and watch providers

| Path | Response quirks |
|------|-----------------|
| `/discover/genreslider/movie` | Bare **array** or `{ results: [...] }` |
| `/discover/genreslider/tv` | Same |
| `/watchproviders/movies?watchRegion=` | Same |
| `/watchproviders/tv?watchRegion=` | Same |

Helpers accept both shapes.

## Details

| Path | Used for |
|------|----------|
| `GET /api/v1/movie/{tmdbId}` | Modal Seerr details, mediaInfo, requests |
| `GET /api/v1/tv/{tmdbId}` | Same + seasons for season picker |

### mediaInfo fields we care about

| Field | Use |
|-------|-----|
| `id` | Seerr Media.id for **issues** (not TMDB) |
| `status` / `status4k` | Availability — [media-status.md](media-status.md) |
| `requests[]` | Active request detection, ownership, change/cancel |
| `externalServiceId` / `externalServiceId4k` | Arr deep links / progress |
| seasons on requests | TV change-request / unmonitor |

After details load, controller runs `QualityCatalogService.AnnotateRequestProfiles` to attach `profileName` / `qualityLabel` onto nested requests.

## Caching

Discover pages and media details use in-memory TTL caches (`JsonMemoryCache` / detail cache ~10 minutes for request-list TMDB details) to avoid hammering Seerr.

## hideAvailable

Discovery matches Seerr’s default of not forcing `hideAvailable=true` unless configured — available titles still appear in browse grids with status overlays.
