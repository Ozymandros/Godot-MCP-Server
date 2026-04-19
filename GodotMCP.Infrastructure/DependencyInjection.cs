using System.Net;
using System.Net.Http;
using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Config;
using GodotMCP.Infrastructure.Integrations;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GodotMCP.Infrastructure;

/// <summary>
/// Registers infrastructure services used by the application and MCP server host.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds concrete infrastructure implementations to the dependency container.
    /// </summary>
    /// <param name="services">Service collection to populate.</param>
    /// <param name="projectRoot">Project root used by path-bound services.</param>
    /// <returns>The same service collection for fluent chaining.</returns>
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
        services.AddSingleton<ICameraService, CameraService>();
        services.AddSingleton<ISceneGraphService, SceneGraphService>();
        services.AddSingleton<IResourcePipelineService, ResourcePipelineService>();
        services.AddSingleton<IUiService, UiService>();
        services.AddSingleton<ILightingService, LightingService>();
        services.AddSingleton<IPhysicsService, PhysicsService>();

        services.AddHttpClient<IGodotEngineDocumentationClient, GodotEngineDocumentationClient>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GodotMCP-Server/1.0 (Godot Engine documentation search)");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All
            });

        return services;
    }
}
