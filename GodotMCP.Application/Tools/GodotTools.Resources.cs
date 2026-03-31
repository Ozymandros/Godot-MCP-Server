using GodotMCP.Core.Models;
using StreamJsonRpc;

namespace GodotMCP.Application.Tools;

public partial class GodotTools
{
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
