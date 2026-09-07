# JustWatch overview

JustWatch does **not** publish a stable public API for third-party apps. JellySeerr posts to the same GraphQL endpoint the JustWatch website uses.

| Item | Value |
|------|-------|
| URL | `https://apis.justwatch.com/graphql` |
| Auth | None |
| Headers | Spoofed desktop Chrome `User-Agent` |
| Code | [`JustWatchQualitiesService.cs`](../../src/Jellyfin.Plugin.JellySeerr/Services/JustWatchQualitiesService.cs) |
| Plugin route | `GET JellySeerr/justwatch/qualities/{mediaType}/{tmdbId}` |

## Pipeline

```text
1. Require TmdbApiKey
2. GET TMDB movie/tv → title + year
3. GraphQL GetSearchResults(searchQuery=title)
4. Pick edge where objectType MOVIE|SHOW, year match, normalized title match
5. If search offers thin → GraphQL GetUrlTitleDetails(fullPath)
6. Map presentationType → Ultra-HD / HD / SD
7. Return highestReleasedQuality + mostCommonQuality
```

Country: advanced JustWatch settings, or `WatchRegion` when `UseWatchRegionForCountry` is enabled.

This integration can break without notice if JustWatch changes operation names or field sets. See [graphql.md](graphql.md).
