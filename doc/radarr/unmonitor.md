# Radarr unmonitor

Code: `ServarrProgressService.UnmonitorAsync` → movie branch.

## Flow

```text
1. Create HttpClient with Radarr URL + X-Api-Key
2. FindByTmdbAsync("movie", tmdbId)
   - GET movie?tmdbId=
   - else GET movie and scan tmdbId
3. Set monitored = false on the JObject
4. PUT /api/v3/movie/{id} with the **full** JSON body
5. Return 200 message: files left on disk
```

## Why full body?

Radarr’s `PUT /api/v3/movie/{id}` expects a complete `MovieResource`. Sending only `{ "monitored": false }` can fail validation or wipe fields. We mutate the previously fetched resource.

## Fallback

If the caller asked for TV first and Sonarr miss, or movie first and Radarr miss, unmonitor tries the **other** Arr product (some mis-tagged requests).

## Permissions

Enforced in `JellySeerrController` before calling the service: admin or owner of a Seerr request on that TMDB id. See [permissions.md](../permissions.md).

## Example PUT body (trimmed)

```json
{
  "id": 12,
  "title": "Fight Club",
  "tmdbId": 550,
  "monitored": false,
  "qualityProfileId": 4,
  "path": "/movies/Fight Club (1999)",
  "rootFolderPath": "/movies",
  "hasFile": true
}
```

(Actual payload is the entire object returned by GET.)
