# Networking, LAN gate, image cache, Seerr proxy

## LAN gate

[`LocalNetworkAccessHelper`](../../src/Jellyfin.Plugin.JellySeerr/Helpers/LocalNetworkAccessHelper.cs)

Used by the controller when deciding `canOpenLocalServices` and whether Arr/Seerr **open URLs** are attached to API responses.

### Rules

1. `HttpContext.Connection.RemoteIpAddress` must be non-null.
2. IPv4-mapped IPv6 → map to IPv4.
3. Client must be **loopback** or **private**:
   - IPv4: `10/8`, `172.16/12`, `192.168/16`
   - IPv6: loopback, link-local, ULA (`fc00::/7`)
4. Collect literal private IP hosts from configured URLs (`JellyseerrUrl`, `ExternalJellyseerrUrl`, `RadarrUrl`, `SonarrUrl`).
5. If **no** literal private hosts → allow (client already private).
6. If some exist → client must share the same IPv4 **/16** (first two octets) with at least one host.

Hostname-only URLs (Docker DNS names) do not add the `/16` constraint.

### Combined with admin

`client-settings.canOpenLocalServices = isAdmin && CanOpenLocalServices()`.  
Non-admins never receive Arr/Seerr deep links even on LAN.

## Image cache

[`ImageCacheService`](../../src/Jellyfin.Plugin.JellySeerr/Services/ImageCacheService.cs) / helpers:

| Setting | Effect |
|---------|--------|
| `DirectBrowserImages` | Skip cache; return TMDB CDN URL |
| `FallbackToOriginalImageUrl` | On cache failure, return original |
| `CacheTimeoutSeconds` / `MaxImageCacheEntries` | Eviction |

Serve path: `GET /JellySeerr/CachedImage/{cacheKey}`.

## Seerr catch-all proxy

`GET|POST /JellySeerr/seerr/{*path}`

- Requires mapped Jellyfin→Seerr user.
- Forwards to Seerr with admin `X-Api-Key` + user `X-Api-User`.
- Useful for future UI features; powerful — treat as authenticated Seerr access under the user’s identity.

## Avatar proxy

`GET /JellySeerr/proxy/avatar?path=`

Only allows path prefixes: `/avatar`, `/avatarproxy`, `/api/v1/avatar`.  
Rejects `..`, `://`, `@` to prevent SSRF/path escape.

## JSON memory caches

Discover pages, request media details, and Servarr library/queue snapshots use short TTLs to protect upstream APIs. Developer mode on the plugin may disable HTTP cache headers for embedded assets.

## Outbound timeouts

`SeerrApiClient` HttpClient timeout: **30 seconds**. Arr clients use similar short timeouts in progress code paths.
