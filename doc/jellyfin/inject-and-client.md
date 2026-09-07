# Inject scripts → plugin routes

How browser code maps to `/JellySeerr/*`. All calls use Jellyfin `ApiClient` (session auth) unless noted.

## Script roles

| File | Role |
|------|------|
| `jellyseerr-tabs.js` | Movies/TV home sections, search integration, display-settings |
| `jellyseerr-nativeui.js` | Card overlays / discover request buttons bridge |
| `jellyseerr-modal.js` | Title details, request/quality/season, watchlist, issue, manage |
| `jellyseerr-requests.js` | Requests tab list, progress, avatars |

## tabs.js

| Call | Route |
|------|-------|
| Discover sections | `GET JellySeerr/discover/...` |
| Genres / providers / studios / networks | matching GET routes |
| Search | `GET JellySeerr/search?query=` |
| Display prefs | `GET JellySeerr/display-settings` |
| Backdrop batch | `POST JellySeerr/backdrops` |
| Session / hide tabs | `GET JellySeerr/session` (indirect via display) |

## modal.js

| Feature | Route |
|---------|-------|
| Client flags + TMDB key | `GET client-settings` |
| Seerr details | `GET details/{mediaType}/{id}` |
| JustWatch lines | `GET justwatch/qualities/...` |
| Backdrop | `GET backdrop/...` |
| Quota | `GET quota` |
| Quality profiles | `GET request-options/{mediaType}` |
| Create / change request | `POST\|PUT request` / `request/{id}` |
| Cancel / approve / decline / retry | DELETE/POST `request/{id}…` |
| Watchlist | POST/DELETE `watchlist` |
| Issue | `POST issue` (`MediaId` = Seerr media id) |
| Unmonitor | `POST servarr/unmonitor` |
| Play lookup | `GET library-item/{mediaType}/{tmdbId}` |
| Browser TMDB | direct `https://api.themoviedb.org/3/...` |

### Request JSON shape (modal → plugin)

```json
{
  "MediaType": "movie",
  "mediaType": "movie",
  "MediaId": 550,
  "mediaId": 550,
  "Is4k": false,
  "ServerId": 0,
  "ProfileId": 4,
  "RootFolder": "/movies",
  "Seasons": [1, 2]
}
```

Dual casing exists so System.Text.Json binders accept either form (`RequestPayload` camel aliases).

## requests.js

| Feature | Route |
|---------|-------|
| Settings / LAN flags | `GET client-settings` |
| List | `GET requests?take=&skip=&filter=&all=` |
| Avatars | `GET proxy/avatar?path=` |

Progress UI reads `item.servarrProgress.{statusKey,percent,qualityLabel,openUrl,…}`.

## External (non-plugin) browser targets

| Target | Purpose |
|--------|---------|
| TMDB API | Modal enrichment when key present |
| `image.tmdb.org` | Posters/backdrops when direct images enabled |
| YouTube / IMDb / TMDB website | Outbound links from details |
| Seerr / Radarr / Sonarr UI | Deep links when `canOpenLocalServices` |

## Custom Tabs / File Transformation

Tabs registration may query `CustomTabs/Config` when that plugin is present. File Transformation registers HTML transforms at startup (`StartupService`) — required for inject; without it, Movies/TV/Requests tabs never appear.
