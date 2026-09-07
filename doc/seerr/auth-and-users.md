# Seerr auth and users

See also [overview.md](overview.md) and [permissions.md](../permissions.md).

## Headers

| Header | Required | Source |
|--------|----------|--------|
| `X-Api-Key` | Yes | Plugin `JellyseerrApiKey` (Seerr Settings → General) |
| `X-Api-User` | When acting as a user | Numeric Seerr user `id` from mapping |

`SeerrApiClient.CreateClient` always sets the API key. `X-Api-User` is added only when `seerrUserId` is non-null.

**Note:** OpenAPI documents cookie auth and `X-Api-Key` only. `X-Api-User` is the established admin-key impersonation mechanism used by Jellyseerr/Seerr API clients so requests consume the correct user’s quota and permissions.

## GET `/api/v1/status`

**Caller:** `ConnectionService` (settings health).

Example response fields used:

```json
{ "version": "2.x.x" }
```

## GET `/api/v1/auth/me`

**Caller:** connection test fallback identity.

```json
{ "displayName": "Admin", "id": 1, "permissions": 2 }
```

With only the API key (no `X-Api-User`), this is typically the admin/API identity.

## GET `/api/v1/user/jellyfin/{jellyfinUserId}`

**Caller:** `UserMappingService.LookupAsync`.

- Path param: Jellyfin user GUID in `D` format (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`).
- Success: single user object with `id`, `permissions`, `displayName` / `username`, `email`.
- Missing link: non-200 / empty → fall through to search.

## GET `/api/v1/user?q={username}`

**Caller:** mapping fallback; also Discovery/RequestList resolve helpers.

```json
{
  "results": [
    {
      "id": 12,
      "jellyfinUsername": "alice",
      "jellyfinUserId": "…",
      "username": "alice",
      "displayName": "Alice",
      "email": "a@example.com",
      "permissions": 262176
    }
  ]
}
```

Match rule: `jellyfinUsername` equals Jellyfin username (ordinal ignore case). First match wins.

### Field map

| Upstream | Plugin |
|----------|--------|
| `id` | `SeerrUserMatch.Id` → `X-Api-User` |
| `permissions` | bit checks in `UserMappingService` |
| `displayName` / `username` | admin users table |

## GET `/api/v1/user/{id}/quota`

**Caller:** controller `quota` route → proxied for the modal quota line.

Returned as-is to the browser; modal reads remaining movie/TV slots when present.

## Errors

| Situation | Plugin behavior |
|-----------|-----------------|
| Seerr URL/key empty | 400 “Seerr is not configured” |
| User not mapped | 400 “Could not match this Jellyfin user…” on mutating routes |
| Mapping HTTP failure | Logged; user treated as unmapped |

## Admin users table

`GET /JellySeerr/admin/users` maps every Jellyfin user through `MapJellyfinUsersAsync` and exposes derived flags (`canRequest`, `canManage`, …) for the plugin config UI.
