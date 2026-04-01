namespace GodotMCP.Core.Models;

/// <summary>
/// In-memory representation of a Godot scene file used by serializers and tools.
/// </summary>
public sealed class GodotScene
{
    /// <summary>Number of load steps for the scene format.</summary>
    public int LoadSteps { get; set; } = 1;

    /// <summary>Scene file format version.</summary>
    public int Format { get; set; } = 3;

    /// <summary>External resource references declared in the scene.</summary>
    public List<ExtResource> ExternalResources { get; } = [];

    /// <summary>Sub-resources embedded in the scene.</summary>
    public List<SubResource> SubResources { get; } = [];

    /// <summary>Top-level nodes in the scene.</summary>
    public List<GodotNode> Nodes { get; } = [];
}

/// <summary>
/// Reference to an external resource (script, texture, etc.) used by a scene.
/// </summary>
public sealed class ExtResource
{
    /// <summary>Identifier used within the scene for the external resource.</summary>
    public required string Id { get; set; }

    /// <summary>Resource type string (for example "Script" or "Texture2D").</summary>
    public required string Type { get; set; }

    /// <summary>Path to the external resource (project-relative, e.g. "res://...").</summary>
    public required string Path { get; set; }
}

/// <summary>
/// Represents a sub-resource embedded inside a scene (e.g., a material instance).
/// </summary>
public sealed class SubResource
{
    /// <summary>Local id of the sub-resource.</summary>
    public required string Id { get; set; }

    /// <summary>Type name of the sub-resource.</summary>
    public required string Type { get; set; }

    /// <summary>Key/value property bag for the sub-resource.</summary>
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Represents a node declared in a Godot scene with its properties and metadata.
/// </summary>
public sealed class GodotNode
{
    /// <summary>Node name as declared in the scene.</summary>
    public required string Name { get; set; }

    /// <summary>Type/class name of the node.</summary>
    public required string Type { get; set; }

    /// <summary>Optional parent node path.</summary>
    public string Parent { get; set; } = string.Empty;

    /// <summary>Optional instance reference for instanced scenes.</summary>
    public string? Instance { get; set; }

    /// <summary>Property dictionary for the node.</summary>
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
}
