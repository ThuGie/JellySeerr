using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.JellySeerr.Configuration;
using Jellyfin.Plugin.JellySeerr.Configuration.Advanced;
using Jellyfin.Plugin.JellySeerr.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public class JustWatchQualitiesService
{
    private const string GraphQlUrl = "https://apis.justwatch.com/graphql";
    private const string UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36";

    // All justwatch qualities and their Seerr tiers (excluding canvas)
    private static readonly Dictionary<string, int> JwQualities = new(StringComparer.Ordinal)
    {
        ["BLURAY_4K"] = 3,
        ["4K"] = 3,
        ["_4K"] = 3,
        ["BLURAY"] = 2,
        ["HD"] = 2,
        ["DVD"] = 1,
        ["SD"] = 1
    };

    // Seerr tiers and their labels
    private static readonly Dictionary<int, string> SeerrQualityLabels = new()
    {
        [3] = "Ultra-HD",
        [2] = "HD - 720p/1080p",
        [1] = "SD"
    };

    private const string SearchQuery = """
        query GetSearchResults($country: Country!, $language: Language!, $first: Int!, $searchQuery: String, $location: String!) {
            searchTitles(country: $country, first: $first, filter: {searchQuery: $searchQuery, includeTitlesWithoutUrl: true}, source: $location) {
                edges {
                    node {
                        objectType
                        content(country: $country, language: $language) {
                            fullPath
                            title
                            originalReleaseYear
                        }
                        offers(country: $country, platform: WEB, filter: {preAffiliate: true, fallbackToForeignOffers: true}) {
                            id
                            presentationType
                        }
                    }
                }
            }
        }
        """;

    private const string PageQuery = """
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
        """;

    private readonly HttpClient _httpClient;
    private readonly ILogger<JustWatchQualitiesService> _logger;

    public JustWatchQualitiesService(HttpClient httpClient, ILogger<JustWatchQualitiesService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<JustWatchQualitiesDto?> GetQualitiesAsync(string mediaType, int tmdbId, CancellationToken cancellationToken = default)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        string? apiKey = config.TmdbApiKey?.Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            return null;
        }

        // Use tmdb title and release year to fetch the correct JustWatch search result
        (string Title, int Year)? metadata = await FetchTmdbMetadataAsync(mediaType, tmdbId, apiKey, cancellationToken)
            .ConfigureAwait(false);
        if (metadata == null)
        {
            return null;
        }

        string jwType = string.Equals(mediaType, "movie", StringComparison.OrdinalIgnoreCase) ? "MOVIE" : "SHOW";
        string normalizedTitle = Normalize(metadata.Value.Title);

        AdvancedJustWatchSettings jwSettings = AdvancedSettingsHelper.Resolve(config).JustWatch;
        JArray? edges = await GetJwSearchEdgesAsync(metadata.Value.Title, jwSettings, cancellationToken).ConfigureAwait(false);
        if (edges == null)
        {
            return null;
        }

        foreach (JToken edgeToken in edges)
        {
            JObject? node = edgeToken["node"] as JObject;
            JObject? content = node?["content"] as JObject;
            if (node == null || content == null)
            {
                continue;
            }

            // Match on normalized title, release year, and media type
            if (!string.Equals(Normalize(content.Value<string>("title")), normalizedTitle, StringComparison.Ordinal) ||
                content.Value<int?>("originalReleaseYear") != metadata.Value.Year ||
                !string.Equals(node.Value<string>("objectType"), jwType, StringComparison.Ordinal))
            {
                continue;
            }

            JArray? nodeOffers = DedupeOffers(node["offers"] as JArray);
            QualityResult? qualities = FindQualities(nodeOffers);

            // Search results dont always include all offers so fetch media page when the list is empty
            if (qualities == null && (nodeOffers == null || nodeOffers.Count == 0))
            {
                string? fullPath = content.Value<string>("fullPath");
                if (string.IsNullOrEmpty(fullPath))
                {
                    return null;
                }

                qualities = FindQualities(await GetJwPageOffersAsync(fullPath, jwSettings, cancellationToken).ConfigureAwait(false));
            }

            return qualities == null ? null : ToDto(qualities.Value);
        }

        return null;
    }

    private static JustWatchQualitiesDto ToDto(QualityResult result) =>
        new()
        {
            HighestReleasedQuality = SeerrQualityLabels[result.Highest],
            MostCommonQuality = SeerrQualityLabels[result.MostCommon]
        };

    private async Task<(string Title, int Year)?> FetchTmdbMetadataAsync(string mediaType, int tmdbId, string apiKey, CancellationToken cancellationToken)
    {
        bool isTv = string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase);
        string segment = isTv ? "tv" : "movie";
        string url = $"https://api.themoviedb.org/3/{segment}/{tmdbId.ToString(CultureInfo.InvariantCulture)}";

        try
        {
            using HttpRequestMessage request = BuildTmdbRequest(url, apiKey);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            JObject payload = JObject.Parse(json);
            string? title = isTv ? payload.Value<string>("name") : payload.Value<string>("title");
            string? dateKey = isTv ? payload.Value<string>("first_air_date") : payload.Value<string>("release_date");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(dateKey))
            {
                return null;
            }

            string yearPart = dateKey.Split('-')[0];
            if (!int.TryParse(yearPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int year))
            {
                return null;
            }

            return (title, year);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "JS • failed to fetch TMDB metadata for JustWatch qualities {Type}/{TmdbId}", mediaType, tmdbId);
            return null;
        }
    }

    private async Task<JArray?> GetJwSearchEdgesAsync(string query, AdvancedJustWatchSettings settings, CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        var payload = new JObject
        {
            ["operationName"] = "GetSearchResults",
            ["query"] = SearchQuery,
            ["variables"] = new JObject
            {
                ["country"] = AdvancedSettingsHelper.ResolveJustWatchCountry(config),
                ["language"] = settings.Language,
                ["searchQuery"] = query,
                ["first"] = settings.SearchResultLimit,
                ["location"] = "SearchSuggester"
            }
        };

        JObject? data = await PostGraphQlAsync(payload, cancellationToken).ConfigureAwait(false);
        return data?["data"]?["searchTitles"]?["edges"] as JArray;
    }

    private async Task<JArray?> GetJwPageOffersAsync(string fullPath, AdvancedJustWatchSettings settings, CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        var payload = new JObject
        {
            ["operationName"] = "GetUrlTitleDetails",
            ["query"] = PageQuery,
            ["variables"] = new JObject
            {
                ["platform"] = "WEB",
                ["fullPath"] = fullPath,
                ["site"] = "www",
                ["country"] = AdvancedSettingsHelper.ResolveJustWatchCountry(config)
            }
        };

        JObject? data = await PostGraphQlAsync(payload, cancellationToken).ConfigureAwait(false);
        return DedupeOffers(data?["data"]?["urlV2"]?["node"]?["offers"] as JArray);
    }

    private async Task<JObject?> PostGraphQlAsync(JObject payload, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlUrl);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            request.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JObject.Parse(json);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "JS • JustWatch GraphQL request failed");
            return null;
        }
    }

    private static HttpRequestMessage BuildTmdbRequest(string url, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (IsBearerToken(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        else
        {
            request.RequestUri = new Uri(AppendQuery(url, "api_key", apiKey));
        }

        return request;
    }

    private static bool IsBearerToken(string apiKey)
    {
        string[] parts = apiKey.Split('.');
        return parts.Length == 3;
    }

    private static string AppendQuery(string url, string name, string value)
    {
        string separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return url + separator + Uri.EscapeDataString(name) + "=" + Uri.EscapeDataString(value);
    }

    private static string Normalize(string? value) =>
        Regex.Replace(value ?? string.Empty, @"\W+", string.Empty).ToLowerInvariant();

    private static JArray? DedupeOffers(JArray? offers)
    {
        if (offers == null)
        {
            return null;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var deduped = new JArray();

        foreach (JToken offerToken in offers)
        {
            if (offerToken is not JObject offer)
            {
                continue;
            }

            if (string.Equals(offer.Value<string>("presentationType"), "CANVAS", StringComparison.Ordinal))
            {
                continue; // Non-quality promo slots just in case
            }

            string? id = offer["id"]?.ToString();
            if (string.IsNullOrEmpty(id) || !seen.Add(id))
            {
                continue;
            }

            deduped.Add(offer);
        }

        return deduped;
    }

    private static QualityResult? FindQualities(JArray? offers)
    {
        var tierCounts = new Dictionary<int, int>
        {
            [1] = 0,
            [2] = 0,
            [3] = 0
        };
        int highest = 0;

        if (offers != null)
        {
            foreach (JToken offerToken in offers)
            {
                if (offerToken is not JObject offer)
                {
                    continue;
                }

                if (!JwQualities.TryGetValue(offer.Value<string>("presentationType") ?? string.Empty, out int tier))
                {
                    continue;
                }

                tierCounts[tier]++;
                highest = Math.Max(highest, tier);
            }
        }

        if (highest == 0)
        {
            return null;
        }

        int mostCommon = tierCounts
            .OrderByDescending(entry => entry.Value)
            .ThenByDescending(entry => entry.Key) // Tie-break for higher quality
            .First()
            .Key;

        return new QualityResult(highest, mostCommon);
    }

    private readonly record struct QualityResult(int Highest, int MostCommon);
}