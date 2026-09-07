using Jellyfin.Plugin.JellySeerr.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.JellySeerr;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient();
        serviceCollection.AddSingleton<SeerrApiClient>();
        serviceCollection.AddSingleton<UserMappingService>();
        serviceCollection.AddSingleton<QualityCatalogService>();
        serviceCollection.AddSingleton<RequestService>();
        serviceCollection.AddSingleton<RequestListService>();
        serviceCollection.AddSingleton<ConnectionService>();
        serviceCollection.AddSingleton<ImageCacheService>(services =>
        {
            IHttpClientFactory httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            return ActivatorUtilities.CreateInstance<ImageCacheService>(services, httpClientFactory.CreateClient());
        });
        serviceCollection.AddSingleton<TmdbBackdropService>(services =>
        {
            IHttpClientFactory httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            return ActivatorUtilities.CreateInstance<TmdbBackdropService>(services, httpClientFactory.CreateClient());
        });
        serviceCollection.AddSingleton<JustWatchQualitiesService>(services =>
        {
            IHttpClientFactory httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            return ActivatorUtilities.CreateInstance<JustWatchQualitiesService>(services, httpClientFactory.CreateClient());
        });
        serviceCollection.AddSingleton<DiscoveryService>();
        serviceCollection.AddSingleton<ServarrProgressService>();
    }
}
