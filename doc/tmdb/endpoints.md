# TMDB endpoints we call

Auth: [auth.md](auth.md). Split of callers: [browser-vs-server.md](browser-vs-server.md).

## Server

| Method | Path | Notes |
|--------|------|-------|
| GET | `/3/discover/movie` | Maps Seerr-style params (`sortBy`→`sort_by`, `genre`→`with_genres`, watch providers, date windows, `with_release_type`) |
| GET | `/3/trending/movie/week` | Alternate trending path |
| GET | `/3/movie/{id}/release_dates` | Filter digital/theatrical release types (`results[].release_dates[].type`) |
| GET | `/3/movie/{id}` or `/3/tv/{id}` | JustWatch title/year (`title`/`name`, `release_date`/`first_air_date`) |
| GET | `/3/{movie\|tv}/{id}/images` | optional `include_image_language`; `backdrops[]` / `logos[]` with `file_path`, `iso_639_1`, `vote_average` |

### Discover field map (into card DTO)

| TMDB | Plugin card |
|------|-------------|
| `id` | TMDB provider id |
| `title` / `original_title` | Name |
| `release_date` | Year + sort |
| `poster_path` / `backdrop_path` | Image URLs |
| `vote_average` | Rating |
| `adult` / `original_language` | Filters |

## Browser (modal)

| Method | Path | Notes |
|--------|------|-------|
| GET | `/3/{movie\|tv}/{id}` | `append_to_response=videos,credits,release_dates\|content_ratings,external_ids,similar,recommendations` |
| GET | `/3/{movie\|tv}/{id}/images` | logos |
| GET | `/3/tv/{id}` | seasons fallback for season picker |

### append_to_response usage

| Append | UI |
|--------|-----|
| `videos` | Trailer (YouTube site) |
| `credits` | Cast row |
| `release_dates` / `content_ratings` | Certifications |
| `external_ids` | IMDb link (`imdb_id`) |
| `similar` / `recommendations` | More like this |

## Image CDN sizes

| Use | Size |
|-----|------|
| Discover posters | `w600_and_h900_bestv2` (configurable) |
| Request cards | `w300` |
| Backdrops | `w780` (configurable) |
| Logos / hero | `original` / large |

Cached via `ImageCacheService` → `/JellySeerr/CachedImage/{key}` unless `DirectBrowserImages` is on.
