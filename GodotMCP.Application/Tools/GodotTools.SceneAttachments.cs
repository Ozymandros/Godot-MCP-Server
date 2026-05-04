using System.Globalization;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using GodotMCP.Core.SceneGraph;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Adds or reuses an <c>ext_resource</c> for <paramref name="resourceFileName"/> and sets <paramref name="propertyKey"/> on <paramref name="nodePath"/>.
    /// </summary>
    private static async Task<ToolResult> AttachExtResourceToSceneNodeAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        string projectPath,
        string sceneFileName,
        string nodePath,
        string resourceFileName,
        string propertyKey,
        string extResourceType,
        string rootType,
        CancellationToken cancellationToken)
    {
        string scenePath;
        try
        {
            scenePath = await EnsureSceneReadyAsync(fileService, pathResolver, projectPath, sceneFileName, rootType, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message, "Use projectPath + /scenes/ + scene file name (with .tscn extension).");
        }

        string resourceAbsPath;
        try
        {
            resourceAbsPath = ResolveProjectFilePath(pathResolver, projectPath, resourceFileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        var resPath = pathResolver.ToGodotResPath(resourceAbsPath);
        var sceneText = await fileService.ReadAsync(scenePath, cancellationToken).ConfigureAwait(false);
        var scene = sceneSerializer.Deserialize(sceneText);
        var pathIndex = SceneNodePathIndex.Build(scene);
        if (!SceneNodePathIndex.TryGetNode(pathIndex, nodePath, out var node) || node is null)
        {
            return new ToolResult(false, $"Node '{nodePath}' not found.");
        }

        ExtResource? reuse = null;
        foreach (var ext in scene.ExternalResources)
        {
            if (string.Equals(ext.Path, resPath, StringComparison.Ordinal))
            {
                reuse = ext;
                break;
            }
        }

        var id = reuse?.Id ?? AllocateNextExtResourceId(scene);
        if (reuse is null)
        {
            scene.ExternalResources.Add(new ExtResource { Id = id, Path = resPath, Type = extResourceType });
        }

        node.Properties[propertyKey.Trim()] = $"ExtResource(\"{id}\")";
        scene.RecomputeLoadSteps();
        await fileService.WriteAsync(scenePath, sceneSerializer.Serialize(scene), cancellationToken).ConfigureAwait(false);
        return new ToolResult(true, $"Set '{propertyKey}' on '{nodePath}' to ExtResource(\"{id}\") ({resPath}).");
    }

    private static string AllocateNextExtResourceId(GodotScene scene)
    {
        var max = 0;
        foreach (var ext in scene.ExternalResources)
        {
            if (int.TryParse(ext.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                max = Math.Max(max, n);
            }
        }

        return (max + 1).ToString(CultureInfo.InvariantCulture);
    }
}
