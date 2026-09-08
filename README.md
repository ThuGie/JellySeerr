# JellySeerr

JellySeerr adds Movies, TV, and Requests tabs to the Jellyfin web client. You can browse Seerr, send a request, and check progress without leaving Jellyfin.

Admins decide which Radarr/Sonarr quality profiles are offered. Click a title for details: request it, add or remove it from the Seerr watchlist, or cancel a pending request.

## Prerequisites

- Jellyfin **10.11.9+** (10.11.x catalog ABI)
- A running Seerr instance with Jellyfin users linked
- The [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) plugin (needed for the extra tabs)

## Install from catalog

1. Open **Dashboard → Plugins → Repositories**.
2. Add a repository with this URL:

```
https://raw.githubusercontent.com/ThuGie/JellySeerr/main/manifest.json
```

3. Open **Plugins**, set the filter to **All**, install **JellySeerr**, then restart Jellyfin.

## First-time setup

1. Open **Dashboard → JellySeerr**.
2. On **Setup**, enter the Seerr URL and API key, then click **Test connection**.
3. If SeerrFin is already on this server, **Import from SeerrFin** copies the URL and keys.
4. Open **Quality**, click **Sync from Seerr**, and uncheck anything you do not want people requesting. The fallbacks at the bottom are optional.
5. On **Requests**, set how often the Requests tab reloads (once a minute by default, or Off).
6. Save, then refresh Jellyfin. Movies, TV, and Requests show up in the home tab bar.

## What users see

- **Movies / TV** — trending, popular, genres, studios, networks, streaming services.
- **Search** — Seerr movie and TV hits in Jellyfin search (can be turned off).
- **Details** — request, trailer, watchlist, similar titles, and a report-issue button after something is requested.
- **Requests** — status and download progress if Radarr/Sonarr are configured. Click a poster to open the same details page.

People who are not linked in Seerr can be hidden from the extra tabs (Users tab).

## Quality profiles

Sync from Seerr, then use the **On** checkbox. That is the main control. Disabled profiles never show up in the request picker and cannot be requested from JellySeerr.

The fallbacks are only used when someone submits a request without picking a profile. Leave them on Seerr default unless you want a specific one.

## Managing requests

Open the title from Requests (or from Movies/TV). Pending items can be cancelled there. Managers can approve, decline, or retry, and can switch between Mine and All.

## FAQ

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
