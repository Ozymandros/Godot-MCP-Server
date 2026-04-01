namespace GodotMCP.Core.Models;

public sealed class GodotScene
{
    public int LoadSteps { get; set; } = 1;
    public int Format { get; set; } = 3;
    public List<ExtResource> ExternalResources { get; } = [];
    public List<SubResource> SubResources { get; } = [];
    public List<GodotNode> Nodes { get; } = [];
}

public sealed class ExtResource
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public required string Path { get; set; }
}

public sealed class SubResource
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
}

public sealed class GodotNode
{
    public required string Name { get; set; }
    public required string Type { get; set; }
    public string Parent { get; set; } = string.Empty;
    public string? Instance { get; set; }
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
}
