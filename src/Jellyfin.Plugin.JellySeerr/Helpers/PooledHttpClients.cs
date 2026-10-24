namespace Jellyfin.Plugin.JellySeerr.Helpers;

/// <summary>Short-lived clients with isolated credentials reuse TCP/TLS connections.</summary>
public static class PooledHttpClients
{
    private static readonly SocketsHttpHandler Handler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(10),
        UseCookies = false
    };

    public static HttpClient Create(Uri? baseAddress = null) => new(Handler, disposeHandler: false)
    {
        BaseAddress = baseAddress,
        Timeout = TimeSpan.FromSeconds(30)
    };
}
