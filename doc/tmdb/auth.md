# TMDB authentication

Official docs: [Authentication (application)](https://developer.themoviedb.org/docs/authentication-application).

## Two credential types

| Credential | Shape | How we send it |
|------------|-------|----------------|
| API Read Access Token (v4) | JWT with **three** `.`-separated segments | `Authorization: Bearer {token}` |
| API Key (v3) | Opaque string (not 3 JWT parts) | Query `?api_key={key}` |

Detection (server + browser):

```csharp
apiKey.Split('.').Length == 3  → Bearer
else                           → api_key query
```

Same logic in:

- `DiscoveryService` TMDB helper
- `TmdbBackdropHelper` / `TmdbBackdropService`
- `JustWatchQualitiesService.BuildTmdbRequest`
- `jellyseerr-modal.js` `isTmdbBearerToken` / `fetchTmdbJson`

## Recommendations

- Prefer the **Read Access Token** in plugin settings (works for v3 routes with Bearer).
- Restrict the key in the TMDB dashboard (app / referrer) because [`client-settings`](../jellyfin/plugin-apis.md) may expose it to the browser for the details modal.
- Never commit real keys; docs use placeholders only.
