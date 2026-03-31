using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using StreamJsonRpc;
using System.Reflection;

namespace GodotMCP.Application.Tools;

public partial class GodotTools
{
    private readonly IGodotFileService fileService;
    private readonly IPathResolver pathResolver;
    private readonly ISceneSerializer sceneSerializer;
    private readonly IResourceSerializer resourceSerializer;
    private readonly IImportFileGenerator importFileGenerator;
    private readonly IProjectConfigService projectConfigService;
    private readonly IGodotCliService godotCliService;
    private readonly IIntegrationInspector integrationInspector;

    public GodotTools(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        IResourceSerializer resourceSerializer,
        IImportFileGenerator importFileGenerator,
        IProjectConfigService projectConfigService,
        IGodotCliService godotCliService,
        IIntegrationInspector integrationInspector)
    {
        this.fileService = fileService;
        this.pathResolver = pathResolver;
        this.sceneSerializer = sceneSerializer;
        this.resourceSerializer = resourceSerializer;
        this.importFileGenerator = importFileGenerator;
        this.projectConfigService = projectConfigService;
        this.godotCliService = godotCliService;
        this.integrationInspector = integrationInspector;
    }

    [JsonRpcMethod("get_server_capabilities")]
    public ToolResult GetServerCapabilities()
    {
        var capabilities = new Dictionary<string, string>
        {
            ["parity_doctrine"] = "mutatis-mutandis",
            ["scenes"] = "create/edit/remove/instantiate",
            ["scripts"] = "gdscript/csharp/create/attach/validate",
            ["imports"] = ".import generation + reimport",
            ["sdk_ecosystem"] = "discover + health verification",
            ["classification"] = string.Join(',', Enum.GetNames<ParityClassification>())
        };

        return new ToolResult(true, "Capabilities enumerated.", capabilities);
    }

    [JsonRpcMethod("health_check")]
    public ToolResult HealthCheck()
    {
        return new ToolResult(true, "ok", new Dictionary<string, string>
        {
            ["status"] = "healthy",
            ["transport"] = "stdio-jsonrpc"
        });
    }

    [JsonRpcMethod("get_server_info")]
    public ToolResult GetServerInfo()
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "0.0.0";

        return new ToolResult(true, "Server info.", new Dictionary<string, string>
        {
            ["name"] = "GodotMCP.Server",
            ["version"] = version,
            ["parity_mode"] = "mutatis-mutandis",
            ["cwd"] = pathResolver.ProjectRoot
        });
    }
}
