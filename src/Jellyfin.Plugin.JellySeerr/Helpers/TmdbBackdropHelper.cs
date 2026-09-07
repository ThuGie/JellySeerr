using System.Globalization;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Helpers;

public sealed class BackdropPickResult
{
    public string? FilePath { get; init; }

    public bool HasEnglishBackdrop { get; init; }
}

public static class TmdbBackdropHelper
{
    public static BackdropPickResult PickBackdrop(IEnumerable<JToken>? backdrops, bool preferNeutral = false)
    {
        if (backdrops == null)
        {
            return new BackdropPickResult();
        }

        List<JObject> valid = backdrops
            .OfType<JObject>()
            .Where(b => !string.IsNullOrEmpty(b.Value<string>("file_path")))
            .ToList();

        if (valid.Count == 0)
        {
            return new BackdropPickResult();
        }

        // Return true if backdrop is English
        static bool IsEnglish(JObject b)
        {
            string? lang = b.Value<string>("iso_639_1");
            return string.Equals(lang, "en", StringComparison.OrdinalIgnoreCase);
        }

        // Return true if backdrop has no language
        static bool HasNoLanguage(JObject b)
        {
            string? lang = b.Value<string>("iso_639_1");
            return string.IsNullOrEmpty(lang);
        }

        // Get path of highest rated backdrop
        static string? TopPath(IEnumerable<JObject> items) => items.OrderByDescending(b => b.Value<float?>("vote_average") ?? 0f).FirstOrDefault()?.Value<string>("file_path");

        // Plain backdrop for modal backgrounds
        if (preferNeutral)
        {
            string? neutralPath = TopPath(valid.Where(HasNoLanguage));
            if (!string.IsNullOrEmpty(neutralPath))
            {
                return new BackdropPickResult { FilePath = neutralPath, HasEnglishBackdrop = false };
            }
        }

        // English backdrops
        string? englishPath = TopPath(valid.Where(IsEnglish));
        if (!string.IsNullOrEmpty(englishPath))
        {
            return new BackdropPickResult { FilePath = englishPath, HasEnglishBackdrop = true };
        }

        // Fallback to non-text backdrop (cards overlay title text on it)
        string? noLangPath = TopPath(valid.Where(HasNoLanguage));
        if (!string.IsNullOrEmpty(noLangPath))
        {
            return new BackdropPickResult { FilePath = noLangPath, HasEnglishBackdrop = false };
        }

        return new BackdropPickResult
        {
            FilePath = TopPath(valid),
            HasEnglishBackdrop = false
        };
    }

    public static async Task<BackdropPickResult> FetchBackdropAsync(HttpClient httpClient, string mediaType, int tmdbId, string apiKey, string? languageFilter = "en,null,en-US", bool preferOriginalLanguage = false, bool preferNeutral = false, CancellationToken cancellationToken = default)
    {
        string segment = string.Equals(mediaType, "tv", StringComparison.OrdinalIgnoreCase) ? "tv" : "movie";
        string baseUrl = $"https://api.themoviedb.org/3/{segment}/{tmdbId.ToString(CultureInfo.InvariantCulture)}/images";
        string filter = string.IsNullOrWhiteSpace(languageFilter) ? "en,null,en-US" : languageFilter;
        string filteredUrl = AppendQuery(baseUrl, "include_image_language", filter);

        BackdropPickResult filtered = await TryFetchBackdrop(httpClient, filteredUrl, apiKey, preferOriginalLanguage, preferNeutral, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(filtered.FilePath))
        {
            return filtered;
        }

        return await TryFetchBackdrop(httpClient, baseUrl, apiKey, preferOriginalLanguage, preferNeutral, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<BackdropPickResult> TryFetchBackdrop(HttpClient httpClient, string url, string apiKey, bool preferOriginalLanguage, bool preferNeutral, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = BuildRequest(url, apiKey);
        using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return new BackdropPickResult();
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        JObject? payload = JObject.Parse(json);
        return PickBackdrop(payload?.Value<JArray>("backdrops"), preferOriginalLanguage, preferNeutral);
    }

    private static BackdropPickResult PickBackdrop(IEnumerable<JToken>? backdrops, bool preferOriginalLanguage, bool preferNeutral)
    {
        // Neutral preference wins over original-language card preference
        if (preferNeutral)
        {
            return PickBackdrop(backdrops, preferNeutral: true);
        }

        if (!preferOriginalLanguage)
        {
            return PickBackdrop(backdrops);
        }

        if (backdrops == null)
        {
            return new BackdropPickResult();
        }

        List<JObject> valid = backdrops
            .OfType<JObject>()
            .Where(b => !string.IsNullOrEmpty(b.Value<string>("file_path")))
            .ToList();

        if (valid.Count == 0)
        {
            return new BackdropPickResult();
        }

        static string? TopPath(IEnumerable<JObject> items) => items.OrderByDescending(b => b.Value<float?>("vote_average") ?? 0f).FirstOrDefault()?.Value<string>("file_path");

        string? originalPath = TopPath(valid.Where(b =>
            !string.IsNullOrEmpty(b.Value<string>("iso_639_1")) &&
            !string.Equals(b.Value<string>("iso_639_1"), "en", StringComparison.OrdinalIgnoreCase)));
        if (!string.IsNullOrEmpty(originalPath))
        {
            return new BackdropPickResult { FilePath = originalPath, HasEnglishBackdrop = false };
        }

        return PickBackdrop(backdrops);
    }

    private static HttpRequestMessage BuildRequest(string url, string apiKey)
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
}