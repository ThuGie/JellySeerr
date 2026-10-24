# Monitored release calendar

Available in **1.0.23.0**. Click the small calendar icon in Jellyfin's top header, the **Release calendar** home tab, or the existing calendar button in Requests. The calendar is a dedicated content page, not a dialog. Its home route includes `jellySeerrTab=calendar` so it can be bookmarked. The icon is available in the classic header and the experimental layout's app bar.

## What appears

- Episodes for monitored series and monitored seasons/episodes on Sonarr instances configured in Seerr, including separate standard, 4K and anime instances when permitted.
- Titles monitored by other users or added directly to Sonarr. A personal Seerr request is not required.
- Month and agenda views, previous/next month, Today, refresh, series/episode search, and an option to hide available files. Small screens start in agenda view.
- Episode/season numbers, episode titles, air times in the browser's local time zone, and whether a file is already available. Agenda cards also show network, instance and overview.
- **Awaiting an air date** for continuing/upcoming monitored series without a known next scheduled episode. Ended series, unmonitored series, and series whose existing seasons are all unmonitored are excluded from that list. Sonarr's metadata determines this state; scheduling updates may arrive later.

Air dates do not guarantee a download. Future seasons with no announced episode dates cannot be placed on the grid. A monitored ended series can still have dated episodes in the selected calendar range. Clicking a title opens TV details when Sonarr supplies its TMDB ID.

This page focuses on monitored TV episodes. The older personal movie/TV calendar API is retained for compatibility.

## Setup and access

Install File Transformation and JellySeerr as described in the README, restart Jellyfin, then hard-refresh the browser. Existing tab configurations gain the enabled Release calendar tab automatically. Administrators can disable or rename it under JellySeerr's tab settings; disabling it also removes the header icon.

Users need a mapped Seerr account and TV request permission. 4K instances follow the user's 4K TV permission and the plugin's Disable 4K setting. This is shared, read-only visibility of the monitored TV library; it does not grant download, deletion or monitoring permissions.

Jellyfin must be able to reach each Sonarr address configured in Seerr. If Seerr uses a Docker-only address, the plugin's direct Sonarr address is used when its API key matches that instance. If Seerr has no Sonarr instances, a configured direct Sonarr connection is used as a fallback. An inaccessible Seerr settings API produces an actionable error.

## Data and performance

`GET /JellySeerr/calendar/monitored?start=<ISO timestamp>&end=<ISO timestamp>` accepts ranges up to 43 days to accommodate a six-week month grid across daylight-saving changes. The browser sends local grid boundaries converted to UTC. Sonarr's `/api/v3/calendar` and `/api/v3/series` provide the data; indexers are not searched.

Results are cached for one minute per connection and range, and identical in-flight requests are shared. Two instances are read at a time. Individual connections time out after 30 seconds; a page lookup has a 40-second budget. Successful results remain visible when another instance fails. Failed instances are listed by their configured display name. No API keys, connection addresses, download URLs or library paths are sent to the browser.

Backend tests cover monitoring filters, date boundaries, regional date parsing, shared request independence, multiple instances, cache sharing, cancellation isolation and tab migration. Browser tests cover the full page, filtering, details links, Fetch errors and calendar icon navigation in classic and modern headers. A local sample-data preview was also visually checked. Live server metadata and custom Jellyfin themes may differ.

Upstream source: [Sonarr calendar controller](https://github.com/Sonarr/Sonarr/blob/develop/src/Sonarr.Api.V3/Calendar/CalendarController.cs).
