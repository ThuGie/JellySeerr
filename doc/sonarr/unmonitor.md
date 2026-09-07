# Sonarr unmonitor

## Flow

```text
1. Find series by TMDB (see tmdb-lookup.md)
2. If seasons[] provided:
   - For each matching seasonNumber, set monitored = false
   - If no season remains monitored, set series.monitored = false
3. Else:
   - series.monitored = false
   - all seasons monitored = false
4. PUT /api/v3/series/{id} with full SeriesResource JSON
5. Files remain on disk
```

## Example (season-scoped)

Request from plugin:

```json
{
  "MediaType": "tv",
  "MediaId": 1396,
  "Seasons": [1, 2]
}
```

Mutated Sonarr payload (conceptual):

```json
{
  "id": 5,
  "tmdbId": 1396,
  "monitored": true,
  "seasons": [
    { "seasonNumber": 0, "monitored": false },
    { "seasonNumber": 1, "monitored": false },
    { "seasonNumber": 2, "monitored": false },
    { "seasonNumber": 3, "monitored": true }
  ]
}
```

## Cancel + unmonitor ordering

On cancel from the modal, unmonitor runs **before** `DELETE /api/v1/request/{id}` so Seerr still attributes the request to the user for the ownership check.

## Fallback

If Sonarr miss, try Radarr movie unmonitor for the same TMDB id (and the reverse for movies). See Radarr [unmonitor.md](../radarr/unmonitor.md).
