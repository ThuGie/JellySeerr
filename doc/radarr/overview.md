# Radarr API overview

Base: `{RadarrUrl}/api/v3/`  
Auth: `X-Api-Key: {RadarrApiKey}` (header). Query `?apikey=` is supported by Radarr but unused here.

## Deep pages

| Doc | Topic |
|-----|--------|
| [endpoints.md](endpoints.md) | Path index |
| [movie-and-queue.md](movie-and-queue.md) | Library, queue, quality, progress |
| [unmonitor.md](unmonitor.md) | PUT full movie resource |

## Client files

- [`ServarrProgressService.cs`](../../src/Jellyfin.Plugin.JellySeerr/Services/ServarrProgressService.cs) — library, queue, quality labels, unmonitor
- [`ConnectionService.cs`](../../src/Jellyfin.Plugin.JellySeerr/Services/ConnectionService.cs) — health chip via `system/status`

## Quality object shape

Queue items and `movieFile.quality` typically nest as `quality.quality.{name,resolution}`. See [movie-and-queue.md](movie-and-queue.md).
