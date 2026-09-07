# JustWatch GraphQL (full)

Unofficial. Copied from plugin constants in `JustWatchQualitiesService`.

## Endpoint

```http
POST https://apis.justwatch.com/graphql
Content-Type: application/json
User-Agent: Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36
```

## Operation: GetSearchResults

### Query text

```graphql
query GetSearchResults($country: Country!, $language: Language!, $first: Int!, $searchQuery: String, $location: String!) {
  searchTitles(
    country: $country
    first: $first
    filter: { searchQuery: $searchQuery, includeTitlesWithoutUrl: true }
    source: $location
  ) {
    edges {
      node {
        objectType
        content(country: $country, language: $language) {
          fullPath
          title
          originalReleaseYear
        }
        offers(country: $country, platform: WEB, filter: { preAffiliate: true, fallbackToForeignOffers: true }) {
          id
          presentationType
        }
      }
    }
  }
}
```

### Example variables

```json
{
  "country": "US",
  "language": "en",
  "searchQuery": "Fight Club",
  "first": 10,
  "location": "SearchSuggester"
}
```

### Response path we read

`data.searchTitles.edges[].node`

| Field | Use |
|-------|-----|
| `objectType` | Must be `MOVIE` or `SHOW` (from mediaType) |
| `content.title` | Normalized equality vs TMDB title |
| `content.originalReleaseYear` | Must equal TMDB year |
| `content.fullPath` | Input to page query |
| `offers[].presentationType` | Quality tier |
| `offers[].id` | Dedupe key |

## Operation: GetUrlTitleDetails

Used when search-node offers are missing/incomplete.

### Query text

```graphql
query GetUrlTitleDetails($fullPath: String!, $site: String, $country: Country!, $platform: Platform! = WEB) {
  urlV2(fullPath: $fullPath, site: $site) {
    node {
      ... on MovieOrShowOrSeason {
        offers(country: $country, platform: $platform, filter: { preAffiliate: true, fallbackToForeignOffers: true }) {
          id
          presentationType
        }
      }
    }
  }
}
```

### Example variables

```json
{
  "fullPath": "/us/movie/fight-club",
  "site": "www",
  "country": "US",
  "platform": "WEB"
}
```

### Response path

`data.urlV2.node.offers[]`

## presentationType → labels

| JustWatch value | Tier | Label returned to UI |
|-----------------|-----:|----------------------|
| `BLURAY_4K`, `4K`, `_4K` | 3 | Ultra-HD |
| `BLURAY`, `HD` | 2 | HD - 720p/1080p |
| `DVD`, `SD` | 1 | SD |
| `CANVAS` | — | **Skipped** |
| anything else | — | Ignored |

### Plugin response DTO

`GET /JellySeerr/justwatch/qualities/{mediaType}/{tmdbId}`:

```json
{
  "highestReleasedQuality": "Ultra-HD",
  "mostCommonQuality": "HD - 720p/1080p"
}
```

Modal shows these under “Highest released quality” / “Most common quality”.

## Failure modes

| Case | Result |
|------|--------|
| No TMDB key | `null` → 404 from controller |
| TMDB metadata miss | null |
| No JustWatch edge match | null |
| GraphQL HTTP error | Logged debug; null |
| Ambiguous titles | Wrong node possible (year+normalize mitigates) |

## Risk

Treat as best-effort enrichment, not a hard dependency for requesting.
