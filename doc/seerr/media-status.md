# Seerr media status and request status

**Source of truth:** Seerr `server/constants/media.ts` (same values in Jellyseerr).  
**Do not trust** OpenAPI / `seerr-api.yml` descriptions that say `6 = DELETED` — that YAML has lagged (still true as of 2026-09-07 on `develop`). It omits `BLOCKLISTED` entirely.

**Plugin canonical maps:** `Helpers/MediaStatusHelper.cs` (+ unit tests in `tests/…/MediaStatusHelperTests.cs`). Inject JS maps in `jellyseerr-modal.js` / `jellyseerr-tabs.js` must stay in sync with that helper.

## MediaStatus (media availability)

| Value | Name | UI label (plugin) |
|------:|------|-------------------|
| 1 | UNKNOWN | Unknown / requestable |
| 2 | PENDING | Pending |
| 3 | PROCESSING | Processing |
| 4 | PARTIALLY_AVAILABLE | Partially Available |
| 5 | AVAILABLE | Available |
| 6 | BLOCKLISTED | Blocklisted |
| 7 | DELETED | Deleted |

4K uses parallel field `status4k` on `media` / `mediaInfo`.

### OpenAPI lag (do not “fix” from YAML)

`seerr-api.yml` `MediaInfo.status` description historically:

> `… 5 = AVAILABLE, 6 = DELETED`

Runtime enum:

```ts
export enum MediaStatus {
  UNKNOWN = 1,
  PENDING,
  PROCESSING,
  PARTIALLY_AVAILABLE,
  AVAILABLE,
  BLOCKLISTED, // ← 6
  DELETED,     // ← 7
}
```

When regenerating clients or auditing against OpenAPI, **ignore** that status description until upstream YAML lists `BLOCKLISTED` and `DELETED` correctly.

### Where we map names → ints

| File | Notes |
|------|-------|
| `MediaStatusHelper` | Canonical C# maps + labels (tested) |
| `DiscoveryService` | Provider ids via helper |
| `jellyseerr-modal.js` `normalizeMediaStatus` | Request button state |
| `jellyseerr-tabs.js` `normalizeDiscoverMediaStatus` | Overlay icon |

Correct aliases: `BLOCKLISTED` / `BLACKLISTED` / `BLOCKED` → **6**; `DELETED` → **7**.

### Request button logic (modal)

| Status | Behavior |
|-------:|----------|
| 5 | Label “Available”, treat as done |
| 6 | Label “Blocklisted”, not requestable |
| 4 | TV may still offer “Request seasons” |
| 2/3 with live request | “Already requested” / pending |
| Stub after cancel (1/2/3, **no** live pending/approved/failed request) | Requestable again |

## MediaRequestStatus (request lifecycle)

| Value | Name | Plugin usage |
|------:|------|----------------|
| 1 | PENDING | Cancel allowed; “Pending” |
| 2 | APPROVED | Cancel often still allowed; progress tracking |
| 3 | DECLINED | Shown as declined |
| 4 | FAILED | Retry (managers); “Failed” chip |
| 5 | COMPLETED | Completed label in list helper |

`RequestListService.GetMediaStatusLabel` → `MediaStatusHelper.GetLabel`; if `requestStatus == 4` and media is not partial/available, forces **Failed**.

## Playable media

`IsPlayableMediaStatus`: media status **4 or 5** (partial or available) before resolving a Jellyfin library item for Play.

## Quirk: empty mediaInfo after cancel

Seerr may leave a `mediaInfo` stub (unknown/processing) with **no** live request rows. Overlay/button code must not treat that as “Already requested”. See comments in `jellyseerr-modal.js` `getRequestButtonState`.
