# Seerr services and quality profiles

Code: [`QualityCatalogService.cs`](../../src/Jellyfin.Plugin.JellySeerr/Services/QualityCatalogService.cs), admin **Sync from Seerr**, request options API.

## List servers

| Path | Role |
|------|------|
| `GET /api/v1/service/radarr` | Preferred list |
| `GET /api/v1/service/sonarr` | Preferred list |
| `GET /api/v1/settings/radarr` | Fallback if service list empty |
| `GET /api/v1/settings/sonarr` | Fallback |

Response may be a **JSON array** or a **single object**. Each server needs `id`.

## Server detail

`GET /api/v1/service/{radarr|sonarr}/{serverId}`

### Fields we read

| Path | Use |
|------|-----|
| `server` / top-level `name` | Display |
| `server.is4k` / `is4k` | Split HD vs 4K catalogs |
| `server.isAnime` | Anime default flag (Sonarr) |
| `server.activeProfileId` | Mark defaults |
| `server.activeDirectory` | Default root folder |
| `profiles[].id` / `profiles[].name` | Quality profiles |
| `rootFolders[].path` | Fallback root |
| `tags` | Advanced request extras |

### Built catalog entry (`QualityProfileEntry`)

Stored in plugin configuration (not re-fetched every request):

| Property | Meaning |
|----------|---------|
| `ServerType` | `radarr` / `sonarr` |
| `ServerId` / `ProfileId` | Pair used in request body |
| `ProfileName` / `DisplayName` | Admin can rename display |
| `Enabled` | Off = hidden and rejected by `IsAllowed` |
| `Is4k` / `IsAnime` | Filtering |
| `IsDefaultMovie` / `Tv` / `*4k` / `Anime` | `ResolveDefault` |
| `DefaultRootFolder` | Applied when client omits root |

Sync preserves admin toggles (`Enabled`, display name, defaults, sort) across refreshes.

## Request options API

`GET /JellySeerr/request-options/{mediaType}` → `RequestService.GetRequestOptionsAsync`:

- Builds options from **enabled** profiles for HD (+ 4K if allowed).
- If user lacks advanced **and** `ExposeProfilesToEveryone` is false → empty `options` array; defaults still applied server-side on submit.
- May load service detail for default server to attach `rootFolders` / `tags`.

### Option JSON (to modal)

```json
{
  "serverId": 0,
  "serverName": "Radarr",
  "is4k": false,
  "isAnime": false,
  "profileId": 4,
  "profileName": "HD-1080p",
  "originalProfileName": "HD-1080p",
  "description": "",
  "rootFolder": "/movies",
  "isDefaultMovie": true
}
```

## Annotating existing requests

`AnnotateRequestProfiles(details)` walks `mediaInfo.requests[]` and sets:

- `profileName` from catalog (or existing)
- `qualityLabel` via `QualityLabelHelper.FromProfile` (SD / 720p / 1080p / 2K / 4K)

Same helper used when mapping the Requests tab list.
