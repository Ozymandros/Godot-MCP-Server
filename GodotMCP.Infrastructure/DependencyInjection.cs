using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Config;
using GodotMCP.Infrastructure.Integrations;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GodotMCP.Infrastructure;

/// <summary>
/// Dependency injection helpers for wiring Godot infrastructure services into an <see cref="IServiceCollection"/>.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Register infrastructure services required by GodotMCP for the given project root.
    /// </summary>
    /// <param name="services">Service collection to extend.</param>
    /// <param name="projectRoot">Absolute or relative project root path.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddGodotInfrastructure(this IServiceCollection services, string projectRoot)
    {
        services.AddSingleton<IPathResolver>(_ => new PathResolver(projectRoot));
        services.AddSingleton<IGodotFileService, GodotFileService>();
        services.AddSingleton<ISceneSerializer, SceneSerializer>();
        services.AddSingleton<IResourceSerializer, ResourceSerializer>();
        services.AddSingleton<IImportFileGenerator, ImportFileGenerator>();
        services.AddSingleton<IProjectConfigService, ProjectConfigService>();
        services.AddSingleton<IGodotCliService, GodotCliService>();
        services.AddSingleton<IGodotOperationsRunner, GodotOperationsRunner>();
        services.AddSingleton<IIntegrationInspector, IntegrationInspector>();
        return services;
    }
}
