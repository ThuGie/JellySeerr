# Radarr movie and queue (deep)

Code: [`ServarrProgressService.cs`](../../src/Jellyfin.Plugin.JellySeerr/Services/ServarrProgressService.cs).  
Auth: `X-Api-Key` on `{RadarrUrl}/api/v3/`.

## GET `/api/v3/system/status`

Used by settings health chips.

```json
{ "version": "5.x.x.x", "instanceName": "Radarr" }
```

## GET `/api/v3/movie`

Full library (cached under key `servarr:radarr:movies` when Servarr cache enabled).

### Fields we index

| Field | Use |
|-------|-----|
| `id` | Queue join / PUT path |
| `tmdbId` | Match Seerr request |
| `monitored` | Progress labels |
| `hasFile` | Downloaded vs missing |
| `status` | Unreleased detection (`announced`, `inCinemas`, …) |
| `sizeOnDisk` | Progress bytes |
| `titleSlug` | Open URL `/movie/{titleSlug}` |
| `movieFile.quality` | Resolution label |
| `movieFile.size` | Fallback size |

## GET `/api/v3/movie?tmdbId={id}`

Documented filter. Used by `FindByTmdbAsync` first; if empty, falls back to full list scan.

## GET `/api/v3/movie/{id}`

Used when Seerr provides `externalServiceId` but the movie was missing from the bulk snapshot.

## GET `/api/v3/queue`

### Query

| Param | Value |
|-------|-------|
| `page` | 1..n |
| `pageSize` | 250 |
| `includeMovie` | `true` |

### Pagination algorithm

```text
records = []
page = 1
loop:
  GET queue?page=&pageSize=250&includeMovie=true
  append response.records
  stop when page * pageSize >= totalRecords or records empty
```

Short TTL cache (~15s) for the aggregated queue when enabled.

### Queue item fields

| Field | Use |
|-------|-----|
| `movieId` | Join to movie |
| `size` / `sizeleft` | Percent complete |
| `quality` | Nested quality (see below) |
| `movie.tmdbId` | Fallback join |
| `movie.titleSlug` | Open URL while queued |

### Progress mapping

| Condition | `statusKey` | Label |
|-----------|-------------|-------|
| Queue items present | `queued` | Queued + % |
| hasFile + monitored | `downloaded-monitored` | Downloaded (Monitored) |
| hasFile + unmonitored | `downloaded-unmonitored` | Downloaded (Unmonitored) |
| monitored + no file | `missing-monitored` | No downloads |
| unmonitored + no file | `missing-unmonitored` | Unmonitored |
| unreleased status | `unreleased` | Unreleased |

## Quality nesting

```json
{
  "quality": {
    "quality": {
      "id": 7,
      "name": "Bluray-1080p",
      "source": "bluray",
      "resolution": 1080
    },
    "revision": { "version": 1, "real": 0, "isRepack": false }
  }
}
```

`ReadArrQuality` reads `quality.quality` (or the object itself), then:

1. `QualityLabelHelper.FromResolution(resolution)`
2. else `QualityLabelHelper.Parse(name)`

Attached onto `servarrProgress.qualityLabel` / `qualityName`.

## Open URL

`{normalizedRadarrBase}/movie/{titleSlug}` — only returned to clients when LAN gate allows.
