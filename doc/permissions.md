# Permissions and access control

JellySeerr enforces two layers:

1. **Seerr permissions** (bitmask on the mapped Seerr user) — used for requests, manage tools, and `X-Api-User` actions.
2. **Jellyfin permissions / LAN** — admin flag, library access, whether Arr/Seerr deep links are returned.

Primary code: [`UserMappingService.cs`](../src/Jellyfin.Plugin.JellySeerr/Services/UserMappingService.cs), [`JellySeerrController.cs`](../src/Jellyfin.Plugin.JellySeerr/Controllers/JellySeerrController.cs), [`jellyseerr-modal.js`](../src/Jellyfin.Plugin.JellySeerr/Inject/jellyseerr-modal.js).

## User mapping

Order:

1. `GET /api/v1/user/jellyfin/{jellyfinUserId:D}` (GUID with dashes).
2. Fallback `GET /api/v1/user?q={username}` — match `jellyfinUsername` (case-insensitive).

Result cached **5 minutes** per Jellyfin user id / username. Unmapped users cannot request; tabs can be hidden via plugin setting.

## Seerr permission bits we use

Aligned with Seerr’s permission flags (subset):

| Constant | Value | Meaning |
|----------|------:|---------|
| `PermissionAdmin` | 2 | Seerr admin (implies manage + request) |
| `PermissionManageRequests` | 16 | Approve / decline / retry / see all |
| `PermissionRequest` | 32 | Generic request |
| `PermissionRequest4k` | 1024 | Generic 4K |
| `PermissionRequest4kMovie` | 2048 | 4K movies |
| `PermissionRequest4kTv` | 4096 | 4K TV |
| `PermissionRequestAdvanced` | 8192 | Pick server/profile (advanced) |
| `PermissionRequestMovie` | 262144 | Movies |
| `PermissionRequestTv` | 524288 | TV |

### Helpers

```text
HasManageRequests(p)  = Admin | ManageRequests
HasRequestAdvanced(p) = Admin | ManageRequests | RequestAdvanced
HasRequestPermission(p, mediaType, is4k):
  Admin → true
  is4k  → Request4k | (Request4kTv | Request4kMovie)
  else  → Request | (RequestTv | RequestMovie)
```

Server-side request submit calls `HasRequestPermission` again before POSTing to Seerr. Profile pickers also call `QualityCatalogService.IsAllowed`.

## Flags returned to the browser

`GET /JellySeerr/client-settings` (and related session payloads) include:

| Field | Meaning |
|-------|---------|
| `isAdmin` | Seerr manage-requests **or** Jellyfin `PermissionKind.IsAdministrator` |
| `canManageRequests` | Seerr manage-requests **and** plugin `EnableManagerTools` |
| `canOpenLocalServices` | `isAdmin` **and** LAN helper allows client IP vs service URLs |
| `canRequest` / `canRequest4k` / `canRequestAdvanced` | From Seerr bits (+ config `ExposeProfilesToEveryone` can widen advanced UI) |
| `tmdbApiKey` | Present when configured (browser TMDB calls) |

## UI action matrix

| Action | Who |
|--------|-----|
| Request / Request 4K | Mapped user with request bits; 4K also blocked if `Disable4k` |
| Change request (quality) | Requester (`requestedBy.id` == mapped Seerr id) **or** admin/manage |
| Cancel request | Same as change (owner or admin) |
| Approve / Decline / Retry | `canManageRequests` |
| Unmonitor in Arr | Owner of a request on that title **or** admin |
| Open in Seerr / Radarr / Sonarr | `canOpenLocalServices` (admin + LAN) |
| Watchlist / Report issue | Mapped user (Seerr enforces further) |
| Requests tab “All” | Manage-requests + `EnableManagerTools` |
| Play | Anyone who can open details **and** title exists in their Jellyfin library (TMDB match) |

### Owner check (modal)

```javascript
// canModifyRequest: admin OR requestedBy.id === session seerr user id
```

### Unmonitor ownership (server)

Controller loads Seerr media details and checks whether any nested request’s `requestedBy.id` equals the mapped user, unless administrator.

## LAN gate (deep links only)

[`LocalNetworkAccessHelper.CanOpenLocalServices`](../src/Jellyfin.Plugin.JellySeerr/Helpers/LocalNetworkAccessHelper.cs):

1. Client IP must be loopback or private (RFC1918 / ULA / link-local).
2. If Seerr/Arr URLs use **literal private IPs**, client must share the same IPv4 `/16` (first two octets) with at least one such host.
3. Hostname-based private URLs (e.g. `http://radarr:7878`) do not add the `/16` constraint beyond “client is private”.

Used when building `client-settings.canOpenLocalServices` and when attaching `servarrProgress.openUrl` / Seerr browse URLs to request payloads.

## Manager tools toggle

Plugin config `EnableManagerTools` must be on for `canManageRequests` to be true in the UI, even if the Seerr user has Manage Requests. Approve/decline/retry and “All requests” depend on this.

## Quality profile exposure

| Config | Behavior |
|--------|----------|
| `ExposeProfilesToEveryone` | Non-advanced users still see the profile picker (options from enabled catalog) |
| Else | Only advanced/manage users get the full option list; others get server defaults applied in `ApplyProfileDefaults` |
| `Disable4k` | Strips 4K profiles and rejects `Is4k` submits with 403 |

## Related

- [architecture.md](architecture.md)
- [seerr/auth-and-users.md](seerr/auth-and-users.md)
- [jellyfin/networking.md](jellyfin/networking.md)
