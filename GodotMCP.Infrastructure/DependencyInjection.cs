using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Config;
using GodotMCP.Infrastructure.Integrations;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GodotMCP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGodotInfrastructure(this IServiceCollection services, string projectRoot)
    {
        services.AddSingleton<IPathResolver>(_ => new PathResolver(projectRoot));
        services.AddSingleton<IGodotFileService, GodotFileService>();
        services.AddSingleton<ISceneSerializer, SceneSerializer>();
        services.AddSingleton<IResourceSerializer, ResourceSerializer>();
        services.AddSingleton<IImportFileGenerator, ImportFileGenerator>();
        services.AddSingleton<IProjectConfigService, ProjectConfigService>();
        services.AddSingleton<IGodotCliService, GodotCliService>();
        services.AddSingleton<IIntegrationInspector, IntegrationInspector>();
        return services;
    }
}
