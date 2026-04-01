using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Integrations;

public sealed class IntegrationInspector(IPathResolver pathResolver) : IIntegrationInspector
{
    public IReadOnlyList<IntegrationMetadata> Discover()
    {
        var addonsPath = Path.Combine(pathResolver.ProjectRoot, "addons");
        if (!Directory.Exists(addonsPath))
        {
            return [];
        }

        var metadata = new List<IntegrationMetadata>();
        foreach (var pluginCfg in Directory.EnumerateFiles(addonsPath, "plugin.cfg", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(Path.GetDirectoryName(pluginCfg) ?? pluginCfg);
            metadata.Add(new IntegrationMetadata
            {
                Name = name,
                Profile = IntegrationProfile.ProjectLocalPlugin,
                Source = pathResolver.ToResPath(pluginCfg),
                GodotVersionRange = "4.x",
                PlatformSupport = "unknown",
                IsMaintained = true
            });
        }

        return metadata;
    }
}
