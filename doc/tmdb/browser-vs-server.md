# TMDB: browser vs server

## Server-side callers

| Service | Why |
|---------|-----|
| `DiscoveryService` | Movie discover with `DiscoverReleaseTypes` filters; attach release windows |
| `TmdbBackdropService` / helper | Pick backdrop/logo for settings / cards |
| `JustWatchQualitiesService` | Title + year for JustWatch matching |

These never send the Seerr key; only `TmdbApiKey` from plugin config.

## Browser-side callers

When `GET /JellySeerr/client-settings` includes a non-empty `tmdbApiKey`, [`jellyseerr-modal.js`](../../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-modal.js):

1. Loads Seerr details via plugin (`/JellySeerr/details/...`).
2. In parallel loads TMDB details with `append_to_response=videos,credits,release_dates|content_ratings,external_ids,similar,recommendations`.
3. Merges cast, trailers, similar, IMDb links, etc. into the modal.
4. May fetch `/images` for logos and `/JellySeerr/backdrop/...` for curated backdrops.
5. Season picker can fall back to `GET /3/tv/{id}` if Seerr seasons are missing.

If no TMDB key is configured, the modal still works with Seerr-only details (reduced richness).

## Image CDN (no auth)

`https://image.tmdb.org/t/p/{size}{path}`

| Size | Typical use |
|------|-------------|
| `w600_and_h900_bestv2` | Discover posters (configurable) |
| `w300` | Request cards |
| `w780` | Backdrops (configurable) |
| `original` | Logos / large hero |

Server may download → cache → `/JellySeerr/CachedImage/{key}` unless `DirectBrowserImages` is enabled. See [jellyfin/networking.md](../jellyfin/networking.md).

## Security note

Exposing TMDB credentials to authenticated Jellyfin users is a deliberate tradeoff for modal UX. It is **not** the Seerr admin key. Still treat TMDB keys as sensitive enough to rotate if leaked publicly.
