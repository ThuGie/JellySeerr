using System.Runtime.Loader;
using Jellyfin.Plugin.JellySeerr.Helpers;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.JellySeerr.Services;

public class StartupService : IScheduledTask
{
    private static readonly Guid IndexHtmlTransformationId = Guid.Parse("3d9e1a70-5c2f-4b8a-ae16-7f4c9b2d0e81");

    private readonly ILogger<JellySeerrPlugin> _logger;

    public StartupService(ILogger<JellySeerrPlugin> logger)
    {
        _logger = logger;
    }

    public string Name => "JellySeerr Startup";

    public string Key => "Jellyfin.Plugin.JellySeerr.Startup";

    public string Description => "Registers file transformations for JellySeerr";

    public string Category => "Startup Services";

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("JS • registering file transformations");

        var fileTransformationAssembly = AssemblyLoadContext.All
            .SelectMany(x => x.Assemblies)
            .FirstOrDefault(x =>
                (x.GetName().Name ?? x.FullName ?? string.Empty)
                    .Contains("FileTransformation", StringComparison.OrdinalIgnoreCase));

        if (fileTransformationAssembly == null)
        {
            _logger.LogWarning("JS • File Transformation plugin not found. UI injection will not work");
            return Task.CompletedTask;
        }

        Type? pluginInterfaceType = fileTransformationAssembly.GetType("Jellyfin.Plugin.FileTransformation.PluginInterface");
        if (pluginInterfaceType == null)
        {
            _logger.LogWarning("JS • File Transformation PluginInterface type not found");
            return Task.CompletedTask;
        }

        var payload = new JObject
        {
            ["id"] = IndexHtmlTransformationId,
            ["fileNamePattern"] = "index.html",
            ["callbackAssembly"] = GetType().Assembly.FullName,
            ["callbackClass"] = typeof(TransformationPatches).FullName,
            ["callbackMethod"] = nameof(TransformationPatches.IndexHtml)
        };

        pluginInterfaceType.GetMethod("RegisterTransformation")?.Invoke(null, new object?[] { payload });
        _logger.LogInformation("JS • registered index.html transformation");
        return Task.CompletedTask;
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger };
    }
}
