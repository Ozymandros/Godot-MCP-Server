using GodotMCP.Core.Models;
using StreamJsonRpc;

namespace GodotMCP.Application.Tools;

public partial class GodotTools
{
    /// <summary>Create a resource file of the given type with the provided properties.</summary>
    [JsonRpcMethod("create_resource")]
    public async Task<ToolResult> CreateResourceAsync(
        string path,
        string type,
        Dictionary<string, string> properties,
        CancellationToken cancellationToken = default)
    {
        await fileService.WriteAsync(path, resourceSerializer.Serialize(type, properties), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Resource created at '{path}'.");
    }
}
