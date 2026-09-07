# JellySeerr

JellySeerr adds **Movies**, **TV**, and **Requests** tabs to the Jellyfin web client so you can discover titles and send requests to [Seerr](https://github.com/Fallenbagel/jellyseerr) without leaving Jellyfin.

Admins choose which Radarr/Sonarr quality profiles users can pick. Users can cancel or edit pending requests. Managers can approve, decline, or retry.

## Prerequisites

- Jellyfin **10.11.x**
- A running Seerr instance with Jellyfin users linked
- The [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) plugin (required for the extra tabs)

## Install from catalog

1. Open **Dashboard → Plugins → Repositories**.
2. Add a repository with this URL:

```
https://raw.githubusercontent.com/ThuGie/JellySeerr/main/manifest.json
```

3. Open **Plugins**, set the filter to **All**, install **JellySeerr**, then restart Jellyfin.

## First-time setup

1. Open **Dashboard → JellySeerr**.
2. On **Setup**, enter your Seerr URL and API key, then click **Test connection**.
3. Optional: **Import from SeerrFin** if that plugin is already configured on the same server.
4. Open **Quality**, click **Sync from Seerr**, enable only the profiles you want, and mark defaults for movie / TV / anime / 4K.
5. Save. After a refresh, Movies, TV, and Requests appear in the home tab bar.

## What users see

- **Movies / TV** — trending, popular, top rated, upcoming, genres, studios/networks, and streaming services.
- **Search** — Seerr movie and TV hits in Jellyfin search (can be turned off).
- **Request modal** — seasons, quality profile, trailer, watchlist, issue report, similar titles.
- **Requests** — status, download progress (if Radarr/Sonarr keys are set), play when the title is in the library, cancel/edit pending items.

Users who are not linked in Seerr can be hidden from the extra tabs (Users tab).

## Quality profiles

JellySeerr does not show every Radarr/Sonarr profile Seerr knows about. After a sync, each profile can be:

- enabled or disabled
- given a friendlier display name
- marked as the default for movies, TV, anime, or 4K

Filtering happens on the server. A disabled profile cannot be requested from the plugin.

## Managing requests

Pending requests can be cancelled or edited (profile, seasons, folder). Once Seerr has approved a request, fields can no longer be changed.

Users with Seerr manage-request permission also get approve, decline, retry, and an **All requests** view.

## FAQ

**A household member sees broken Movies/TV tabs.**  
They need a matching Seerr user (`jellyfinUsername` or Jellyfin user id). Enable **Hide JellySeerr tabs for users who are not linked**.

**Edit failed with a conflict.**  
Only **pending** requests can be edited. Approved requests have to be managed in Seerr.

**4K still appears.**  
Turn on **Disable 4K requests in JellySeerr** on the Quality tab, or disable the 4K profiles after sync.

**Tabs do not appear behind a reverse proxy.**  
JellySeerr injects scripts with relative URLs (`../JellySeerr/...`) so a Jellyfin base path still works. Confirm File Transformation is installed and active.

## Build from source

```bash
dotnet build JellySeerr.sln -c Release
```

Copy `Jellyfin.Plugin.JellySeerr.dll`, `meta.json`, and `thumb.png` into a Jellyfin plugins folder, then restart.

## Releasing

Push a version tag such as `v1.0.0.0`. GitHub Actions builds the zip, updates `manifest.json` on `main`, and publishes a GitHub release.

## License

MIT
