# Loading review — 2026-10-24

Reviewed discovery/search mapping, cached images, title details, request enrichment, Arr progress, download/season selection, shared caches, HTTP lifetimes and browser refresh behavior. These changes are based on source inspection and simulated API/DOM tests. No wall-clock speedup or live-server load measurement is claimed.

## Changes

| Path | Previous work | Updated behavior |
| --- | --- | --- |
| Discovery cards | Mapping blocked on every uncached poster/backdrop download | Mapping registers stable image URLs with no network I/O; the image endpoint downloads requested images asynchronously |
| Cached images | Duplicate callers could start multiple downloads; synchronous refresh; index rewritten per download | One shared fetch per source, at most six downloads at once, atomic image writes and batched atomic index updates |
| Browser image reuse | Cache metadata was computed but unused by the image controller | Cache-Control, Last-Modified and ETag are returned; failed refreshes can serve an existing stale image or use the configured original-URL fallback |
| Upstream HTTP | Frequent new connection pools and undisposed TMDB clients | Isolated client headers reuse TCP/TLS connections, with periodic connection renewal and bounded connection/request timeouts |
| Discovery rows | Each row repeated the username-to-Seerr lookup | Five-minute scoped lookup cache plus one shared lookup during concurrent row loads |
| Requests | Metadata fetches could duplicate; shared details were cached without a user/server scope | Scoped, cloned metadata; shared active fetches; at most six simultaneous metadata lookups; bounded shared JSON cache |
| Request progress | Radarr then Sonarr; both queried even on single-media pages; TV series loaded sequentially | Only required services are queried, in parallel; up to four series episode lookups run concurrently |
| Title details | Settings refetched for every open; status waited for settings; optional artwork delayed the dialog | Settings fetched once concurrently and cached for 30 seconds per user; Seerr starts immediately; artwork updates only its connected dialog after metadata renders |
| TMDB metadata | Repeat title opens refetched identical JSON | Shared active promises plus a bounded five-minute browser cache; failed fetches can retry |
| Modal navigation | A late response could overwrite a newer title or act after closing | A generation check rejects superseded results and stale reloads |
| Quotas | Concurrent reads duplicated requests; cache was not scoped to the signed-in user | Shared active reads and user-scoped cache |
| Season selection | Each snapshot fetched the episode list twice, then files one at a time | One episode list supplies both checks; up to four file lookups run concurrently |
| Mutation freshness | Successful Seerr mutations could leave cached request/discovery data visible | Successful writes invalidate request, discovery and request-detail caches |

Final release grabs, permissions, ownership, season coverage, file snapshots and queue checks remain fresh and are not replaced by cached decisions.

## Verification

- A cold batch of 20 cards (40 poster/backdrop registrations) starts zero image downloads while generating URLs.
- Twenty concurrent readers of one uncached image cause one upstream fetch; subsequent warm reads cause none.
- A cancelled waiter does not cancel other readers sharing the same work. Failed work can be retried.
- Bounded asynchronous mapping reaches four concurrent operations, never exceeds four and preserves result order.
- Cache keys distinguish upstream URL, API key and Seerr user without putting credentials in the key text.
- Browser tests cover shared settings/TMDB work, user changes, artwork that remains pending while details become ready, and out-of-order modal responses.
- Existing release-selection, episode, shared-season, failure-handling and permission tests continue to run.

Commands: `dotnet test JellySeerr.sln --configuration Release`, `npm ci --prefix tests/ui`, `npm test --prefix tests/ui`.

## Remaining limits and live checks

Some legacy discovery methods still synchronously wait for upstream JSON. Removing image I/O from that path and reusing connections reduces work, but converting every native/legacy discovery caller to async needs a separate interface refactor. TMDB release-type filtering can still require a lookup per movie. Request/library matching also depends on the Jellyfin library and per-user access rules.

Cold image downloads now move to the first actual image request; they are not eliminated. Very large seasons still need file metadata and fresh safety checks. Release searches depend on indexer response times and are deliberately refreshed before a grab.

The existing viewport lazy-loading for discovery rows and hidden-tab refresh suppression were retained. To establish real latency improvements, compare cold/warm Movies/TV, details and Requests timings on the installed server, record Seerr/TMDB/Arr request counts, and test several simultaneous users. A live Jellyfin/Seerr/Arr installation was not available in this workspace.
