using Jellyfin.Plugin.JellySeerr.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.JellySeerr;

public class JellySeerrPlugin : BasePlugin<PluginConfiguration>, IHasPluginConfiguration, IHasWebPages
{
    public const string PluginGuid = "8f3a2c91-4e7b-4d6a-9c18-2b5e0f7a6d44";

    public override Guid Id => Guid.Parse(PluginGuid);

    public override string Name => "JellySeerr";

    public override string Description =>
        "Discover and request movies and TV shows through Seerr, with curated quality profiles and request management.";

    public static JellySeerrPlugin Instance { get; private set; } = null!;

    public JellySeerrPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        Configuration.Tabs = TabConfigHelper.Normalize(Configuration.Tabs);
        Configuration.TabBarOrder = TabConfigHelper.NormalizeBarOrder(Configuration.TabBarOrder);
        Configuration.QualityProfiles ??= new List<QualityProfileEntry>();
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        string? prefix = GetType().Namespace;
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{prefix}.Configuration.config.html",
            EnableInMainMenu = true,
            DisplayName = "JellySeerr",
            MenuIcon = "theaters"
        };
    }

    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        if (configuration is PluginConfiguration config)
        {
            config.Tabs = TabConfigHelper.Normalize(config.Tabs);
            config.TabBarOrder = TabConfigHelper.NormalizeBarOrder(config.TabBarOrder);
            config.QualityProfiles ??= new List<QualityProfileEntry>();
        }

        base.UpdateConfiguration(configuration);
    }

    public void BustCache()
    {
        Configuration.CacheBustCounter++;
        SaveConfiguration();
    }
}
