using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Compares two scenes and returns structural and property-level differences.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="sceneSerializer">Scene serializer used for parsing input files.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileNameA">First scene file name or relative path under <c>projectPath</c>.</param>
    /// <param name="fileNameB">Second scene file name or relative path under <c>projectPath</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing scene diff payload.</returns>
    [McpServerTool(Name = "diff_scenes"), Description("Compare two Godot scenes and return a structured list of differences.")]
    public static async Task<ToolResult> DiffScenesAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("First scene file name or relative path under projectPath."), Required] string fileNameA,
        [Description("Second scene file name or relative path under projectPath."), Required] string fileNameB,
        CancellationToken cancellationToken = default)
    {
        string scenePathA;
        string scenePathB;
        try
        {
            scenePathA = ResolveProjectFilePath(pathResolver, projectPath, fileNameA);
            scenePathB = ResolveProjectFilePath(pathResolver, projectPath, fileNameB);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var sceneAText = await fileService.ReadAsync(scenePathA, cancellationToken).ConfigureAwait(false);
        var sceneBText = await fileService.ReadAsync(scenePathB, cancellationToken).ConfigureAwait(false);
        var sceneA = sceneSerializer.Deserialize(sceneAText);
        var sceneB = sceneSerializer.Deserialize(sceneBText);

        var diff = new SceneDiffModel();

        // Node changes
        var nodesA = sceneA.Nodes.ToDictionary(n => n.Name, n => n);
        var nodesB = sceneB.Nodes.ToDictionary(n => n.Name, n => n);

        foreach (var nodeName in nodesA.Keys.Except(nodesB.Keys))
        {
            diff.RemovedNodes.Add(nodeName);
        }
        foreach (var nodeName in nodesB.Keys.Except(nodesA.Keys))
        {
            diff.AddedNodes.Add(nodesB[nodeName]);
        }
        foreach (var nodeName in nodesA.Keys.Intersect(nodesB.Keys))
        {
            var nodeA = nodesA[nodeName];
            var nodeB = nodesB[nodeName];
            if (nodeA.Type != nodeB.Type || nodeA.Parent != nodeB.Parent || nodeA.Instance != nodeB.Instance)
            {
                diff.ModifiedNodes.Add(new PropertyChange { Target = nodeName, Property = "Core Attributes", OldValue = "Modified" });
            }

            // Compare properties
            foreach (var prop in nodeA.Properties.Keys.Except(nodeB.Properties.Keys))
            {
                diff.ModifiedNodes.Add(new PropertyChange { Target = nodeName, Property = prop, OldValue = nodeA.Properties[prop], NewValue = null });
            }
            foreach (var prop in nodeB.Properties.Keys.Except(nodeA.Properties.Keys))
            {
                diff.ModifiedNodes.Add(new PropertyChange { Target = nodeName, Property = prop, OldValue = null, NewValue = nodeB.Properties[prop] });
            }
            foreach (var prop in nodeA.Properties.Keys.Intersect(nodeB.Properties.Keys))
            {
                if (nodeA.Properties[prop] != nodeB.Properties[prop])
                {
                    diff.ModifiedNodes.Add(new PropertyChange { Target = nodeName, Property = prop, OldValue = nodeA.Properties[prop], NewValue = nodeB.Properties[prop] });
                }
            }
        }

        return new ToolResult(true, "Scene diff completed.", diff);
    }
}

/// <summary>
/// Describes added, removed, and modified node differences between two scenes.
/// </summary>
public sealed class SceneDiffModel
{
    /// <summary>
    /// Gets removed node names from the first scene.
    /// </summary>
    public List<string> RemovedNodes { get; } = [];

    /// <summary>
    /// Gets nodes added in the second scene.
    /// </summary>
    public List<GodotNode> AddedNodes { get; } = [];

    /// <summary>
    /// Gets modified property entries between matching nodes.
    /// </summary>
    public List<PropertyChange> ModifiedNodes { get; } = [];
}

/// <summary>
/// Describes a single property-level change on a scene node.
/// </summary>
public sealed class PropertyChange
{
    /// <summary>
    /// Gets or sets the node identifier that changed.
    /// </summary>
    public required string Target { get; set; }

    /// <summary>
    /// Gets or sets the property name that changed.
    /// </summary>
    public required string Property { get; set; }

    /// <summary>
    /// Gets or sets the previous value, when available.
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// Gets or sets the new value, when available.
    /// </summary>
    public string? NewValue { get; set; }
}
