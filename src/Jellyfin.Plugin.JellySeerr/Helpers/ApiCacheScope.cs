using System.Security.Cryptography;
using System.Text;

namespace Jellyfin.Plugin.JellySeerr.Helpers;

public static class ApiCacheScope
{
    public static string ForClient(HttpClient client)
    {
        string Header(string name) => client.DefaultRequestHeaders.TryGetValues(name, out var values) ? string.Join(",", values) : "";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{client.BaseAddress}|{Header("X-Api-Key")}|{Header("X-Api-User")}")));
    }
}
