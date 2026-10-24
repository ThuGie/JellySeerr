# JellySeerr

JellySeerr adds Movies, TV, and Requests tabs to the Jellyfin web client. You can browse Seerr, send a request, and check progress without leaving Jellyfin.

> **Required dependency:** Install the [File Transformation plugin](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) as well as JellySeerr. Without it, JellySeerr's Movies, TV, and Requests tabs will not appear. Follow both installation steps below.

Admins decide which Radarr/Sonarr quality profiles are offered. Click a title for details: request it, add or remove it from the Seerr watchlist, or cancel a pending request.

**Advanced download** lets requesters choose a specific movie or episode release for their approved or completed requests, without administrator approval. **Replace whole season** also works on other users' approved TV requests, with an affected-episode list and confirmation. Search results show quality, size, indexer, seeders and rejection reasons. Optionally mark the original releases as bad so Sonarr/Radarr blocklists those releases, not the indexer. Old files are removed only after the chosen replacement is accepted by the download client. [Usage and limitations](doc/advanced-downloads.md).

Loading and caching improvements are documented in the [performance review](doc/performance-review.md), including what was tested and which paths still need live-server measurements.

## Prerequisites

- Jellyfin **10.11.9+** (10.11.x catalog ABI)
- A running Seerr instance with Jellyfin users linked
- The [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) plugin (needed for the extra tabs)

## Install from catalog

### 1. Install File Transformation (required)

1. Open **Dashboard → Plugins → Repositories** and add the File Transformation repository:

```
https://www.iamparadox.dev/jellyfin/plugins/manifest.json
```

2. Open the plugin catalog, find **File Transformation**, and install the version compatible with your Jellyfin server.
3. Restart Jellyfin. File Transformation requires no configuration.

These steps follow the dependency's [official installation instructions](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation#installation).

### 2. Install JellySeerr

1. Return to **Dashboard → Plugins → Repositories**.
2. Add the JellySeerr repository:

```
https://raw.githubusercontent.com/ThuGie/JellySeerr/main/manifest.json
```

3. Open **Plugins**, set the filter to **All**, install **JellySeerr**, then restart Jellyfin.
4. Complete the setup below, then reload the Jellyfin web client. If an existing browser session still shows the old interface, perform a hard refresh (**Ctrl+F5**) or clear the site's browser cache.

## First-time setup

1. Open **Dashboard → JellySeerr**.
2. On **Setup**, enter the Seerr URL and API key, then click **Test connection**.
3. If SeerrFin is already on this server, **Import from SeerrFin** copies the URL and keys.
4. Open **Quality**, click **Sync from Seerr**, and uncheck anything you do not want people requesting. The fallbacks at the bottom are optional.
5. On **Requests**, set how often the Requests tab reloads (once a minute by default, or Off).
6. Save, then refresh Jellyfin. Movies, TV, and Requests show up in the home tab bar.

## What users see

- **Release calendar** — click the small calendar icon in the top header or the Release calendar tab for a full page of monitored Sonarr episodes. Includes month/agenda views, local air times, title filters, and series awaiting an air date. It includes other users' monitored titles. [Calendar guide](doc/monitored-calendar.md).

The [user tools guide](doc/user-tools.md) covers the release calendar, personal notification preferences, categorized issue reporting and comments, replacement history, release comparison, and failed/stalled download recovery. Calendar and Notifications are in Requests; the other tools are in title details and Advanced download.

- **Movies / TV** — trending, popular, genres, studios, networks, streaming services.
- **Search** — Seerr movie and TV hits in Jellyfin search (can be turned off).
- **Details** — request, trailer, watchlist, similar titles, and a report-issue button after something is requested.
- **Requests** — status and download progress if Radarr/Sonarr are configured. Click a poster to open the same details page.
- **Episodes & status / Download status** — see why a request is waiting, check each approved episode's availability, and search selected aired, monitored missing episodes without deleting existing files. [Availability guide](doc/availability.md).

People who are not linked in Seerr can be hidden from the extra tabs (Users tab).

## Quality profiles

Sync from Seerr, then use the **On** checkbox. That is the main control. Disabled profiles never show up in the request picker and cannot be requested from JellySeerr.

The fallbacks are only used when someone submits a request without picking a profile. Leave them on Seerr default unless you want a specific one.

## Managing requests

Open the title from Requests (or from Movies/TV). Pending items can be cancelled there. Managers can approve, decline, or retry, and can switch between Mine and All.

## FAQ

**JellySeerr is installed, but Movies / TV / Requests do not appear.**

First check **Dashboard → Plugins** for **File Transformation**. It is a separate required plugin, not included with JellySeerr. Install it using the repository above, restart Jellyfin, and hard-refresh the browser. Also confirm that your Jellyfin user is linked in Seerr and that the tabs are enabled in JellySeerr's settings. These additions are for the Jellyfin web client; native clients do not automatically gain these tabs.

**A household member sees broken Movies/TV tabs.**  
They need a matching Seerr user (`jellyfinUsername` or Jellyfin user id). Enable **Hide JellySeerr tabs for users who are not linked**.

**The Requests tab keeps reloading.**  
Dashboard → JellySeerr → Requests → Auto-refresh. Set it to Off if you only want the reload button.

**4K still appears.**  
Turn on **Disable 4K requests in JellySeerr** on the Quality tab, or uncheck the 4K profiles after sync.

**Tabs do not appear behind a reverse proxy.**  
Scripts are injected with relative URLs (`../JellySeerr/...`) so a Jellyfin base path still works. Confirm File Transformation is installed and active.

## Build from source

```bash
dotnet build JellySeerr.sln -c Release
```

Copy `Jellyfin.Plugin.JellySeerr.dll`, `meta.json`, and `thumb.png` into a Jellyfin plugins folder, then restart.

## Developer docs

Advanced integration reference (architecture, permissions, request/response field maps, audit) lives in [`doc/`](doc/README.md).

## Releasing

Push a version tag such as `v1.0.1.0`. GitHub Actions builds the zip, updates `manifest.json` on `main`, and publishes a GitHub release.

## License

MIT
