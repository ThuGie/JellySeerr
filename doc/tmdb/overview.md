# TMDB API overview

Base: `https://api.themoviedb.org/3/`  
Image CDN: `https://image.tmdb.org/t/p/{size}{path}`

## Deep pages

| Doc | Topic |
|-----|--------|
| [auth.md](auth.md) | Bearer vs `api_key` |
| [browser-vs-server.md](browser-vs-server.md) | Who calls what |
| [endpoints.md](endpoints.md) | Path index |

## Config

Plugin setting `TmdbApiKey` — Accepts v3 API key **or** v4 Read Access Token (JWT). Detection: [auth.md](auth.md).
