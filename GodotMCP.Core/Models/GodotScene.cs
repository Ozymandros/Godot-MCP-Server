namespace GodotMCP.Core.Models;

/// <summary>
/// Represents a parsed Godot scene file model.
/// </summary>
public sealed class GodotScene
{
    /// <summary>
    /// Gets or sets declared scene load steps.
    /// </summary>
    public int LoadSteps { get; set; } = 1;

    /// <summary>
    /// Gets or sets scene serialization format version.
    /// </summary>
    public int Format { get; set; } = 3;

    /// <summary>
    /// Gets external resource declarations.
    /// </summary>
    public List<ExtResource> ExternalResources { get; } = [];

    /// <summary>
    /// Gets sub-resource declarations.
    /// </summary>
    public List<SubResource> SubResources { get; } = [];

    /// <summary>
    /// Gets scene node declarations.
    /// </summary>
    public List<GodotNode> Nodes { get; } = [];

    /// <summary>
    /// Gets signal connection declarations (<c>[connection ...]</c>).
    /// </summary>
    public List<GodotConnection> Connections { get; } = [];

    /// <summary>
    /// Sets <see cref="LoadSteps"/> to <c>1 + external + sub</c> resources, matching common Godot 4 scene headers.
    /// </summary>
    public void RecomputeLoadSteps()
        => LoadSteps = Math.Max(1, 1 + ExternalResources.Count + SubResources.Count);
}

/// <summary>
/// Represents a <c>[connection ...]</c> line in a scene file.
/// </summary>
public sealed class GodotConnection
{
    /// <summary>
    /// Gets header attributes (signal, from, to, method, flags, etc.).
    /// </summary>
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Represents an <c>ext_resource</c> scene section.
/// </summary>
public sealed class ExtResource
{
    /// <summary>
    /// Gets or sets resource identifier.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets resource type.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets project resource path.
    /// </summary>
    public required string Path { get; set; }
}

/// <summary>
/// Represents a <c>sub_resource</c> scene section.
/// </summary>
public sealed class SubResource
{
    /// <summary>
    /// Gets or sets resource identifier.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets resource type.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Gets serialized sub-resource properties.
    /// </summary>
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Represents a node section in a scene file.
/// </summary>
public sealed class GodotNode
{
    /// <summary>
    /// Gets or sets node name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets node type.
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Gets or sets serialized parent path token.
    /// </summary>
    public string Parent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets optional instance reference.
    /// </summary>
    public string? Instance { get; set; }

    /// <summary>
    /// Gets serialized node properties.
    /// </summary>
    public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
}
