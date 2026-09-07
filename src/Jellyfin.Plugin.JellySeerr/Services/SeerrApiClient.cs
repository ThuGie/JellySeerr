using System.Text;
using Jellyfin.Plugin.JellySeerr.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public class SeerrApiClient
{
    private readonly ILogger<SeerrApiClient> _logger;

    public SeerrApiClient(ILogger<SeerrApiClient> logger)
    {
        _logger = logger;
    }

    public bool IsConfigured(PluginConfiguration? config = null)
    {
        config ??= JellySeerrPlugin.Instance.Configuration;
        return !string.IsNullOrWhiteSpace(config.JellyseerrUrl) && !string.IsNullOrWhiteSpace(config.JellyseerrApiKey);
    }

    public HttpClient CreateClient(PluginConfiguration? config = null, int? seerrUserId = null)
    {
        config ??= JellySeerrPlugin.Instance.Configuration;
        HttpClient client = new()
        {
            BaseAddress = new Uri(config.JellyseerrUrl!.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Add("X-Api-Key", config.JellyseerrApiKey);
        if (seerrUserId != null)
        {
            client.DefaultRequestHeaders.Add("X-Api-User", seerrUserId.Value.ToString());
        }

        return client;
    }

    public async Task<(int StatusCode, string Body, string ContentType)> SendAsync(
        HttpMethod method,
        string relativePath,
        string? body,
        int? seerrUserId,
        CancellationToken cancellationToken)
    {
        PluginConfiguration config = JellySeerrPlugin.Instance.Configuration;
        if (!IsConfigured(config))
        {
            return (400, "{\"message\":\"Seerr is not configured in JellySeerr.\"}", "application/json");
        }

        using HttpClient client = CreateClient(config, seerrUserId);
        string apiPath = relativePath.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase)
            ? relativePath
            : $"/api/v1/{relativePath.TrimStart('/')}";

        using HttpRequestMessage request = new(method, apiPath.TrimStart('/'));
        if (body != null && method != HttpMethod.Get && method != HttpMethod.Head)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";
            return ((int)response.StatusCode, responseBody, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JS • Seerr request failed for {Path}", apiPath);
            return (502, "{\"message\":\"Failed to reach Seerr.\"}", "application/json");
        }
    }

    public async Task<JToken?> GetJsonAsync(string relativePath, int? seerrUserId, CancellationToken cancellationToken)
    {
        (int status, string body, _) = await SendAsync(HttpMethod.Get, relativePath, null, seerrUserId, cancellationToken)
            .ConfigureAwait(false);
        if (status < 200 || status >= 300 || string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JToken.Parse(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JS • failed to parse Seerr JSON from {Path}", relativePath);
            return null;
        }
    }
}
