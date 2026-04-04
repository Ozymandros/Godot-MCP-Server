using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    [McpServerTool(Name = "diff_scenes"), Description("Compare two Godot scenes and return a structured list of differences.")]
    public static async Task<ToolResult> DiffScenesAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        [Description("Project path (res://...) to the first scene file.")] string scenePathA, 
        [Description("Project path (res://...) to the second scene file.")] string scenePathB, 
        CancellationToken cancellationToken = default)
    {
        if (!IsValidResPath(pathResolver, scenePathA) || !IsValidResPath(pathResolver, scenePathB))
        {
            return Invalid("Both paths must be valid project-relative paths.");
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

public sealed class SceneDiffModel
{
    public List<string> RemovedNodes { get; } = [];
    public List<GodotNode> AddedNodes { get; } = [];
    public List<PropertyChange> ModifiedNodes { get; } = [];
}

public sealed class PropertyChange
{
    public required string Target { get; set; }
    public required string Property { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
