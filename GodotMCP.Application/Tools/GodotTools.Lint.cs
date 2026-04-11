using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    [McpServerTool(Name = "lint_project"), Description("Scan the project for potential issues and report them.")]
    public static async Task<ToolResult> LintProjectAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        ISceneSerializer sceneSerializer,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<LintIssue>();
        var root = pathResolver.ProjectRoot;

        // Check for missing .import files for common asset types.
        var assetExtensions = new[] { ".png", ".jpg", ".wav", ".mp3", ".ogg", ".fbx", ".obj", ".glb", ".gltf" };
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (assetExtensions.Contains(ext))
            {
                var importFile = $"{file}.import";
                if (!File.Exists(importFile))
                {
                    issues.Add(new LintIssue
                    {
                        Path = pathResolver.ToResPath(file),
                        Severity = "Error",
                        Message = "Asset missing .import file.",
                        SuggestedFix = "Run 'generate_import_file' for this asset or re-import in Godot."
                    });
                }
            }
        }

        // Check scene resources.
        foreach (var sceneFile in Directory.EnumerateFiles(root, "*.tscn", SearchOption.AllDirectories))
        {
            var resPath = pathResolver.ToResPath(sceneFile);
            try
            {
                var content = await fileService.ReadAsync(resPath, cancellationToken).ConfigureAwait(false);
                var scene = sceneSerializer.Deserialize(content);

                foreach (var ext in scene.ExternalResources)
                {
                    if (!IsValidResPath(pathResolver, ext.Path))
                    {
                        issues.Add(new LintIssue
                        {
                            Path = resPath,
                            Severity = "Error",
                            Message = $"External resource '{ext.Path}' is missing or has an invalid path.",
                            SuggestedFix = "Check res:// path correctly or update ExtResource path."
                        });
                    }
                    else
                    {
                        // Check if the file actually exists on disk.
                        var fullPath = pathResolver.ResolveResPath(ext.Path);
                        if (!File.Exists(fullPath))
                        {
                            issues.Add(new LintIssue
                            {
                                Path = resPath,
                                Severity = "Warning",
                                Message = $"External resource '{ext.Path}' defined in scene does not exist on disk.",
                                SuggestedFix = "Ensure the resource file exists at the specified path."
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                issues.Add(new LintIssue
                {
                    Path = resPath,
                    Severity = "Error",
                    Message = $"Failed to parse scene: {ex.Message}",
                    SuggestedFix = "Ensure scene is a valid .tscn file."
                });
            }
        }

        return new ToolResult(true, $"Lint completed. Found {issues.Count} issue(s).", issues);
    }
}

public sealed class LintIssue
{
    public required string Path { get; set; }
    public required string Severity { get; set; }
    public required string Message { get; set; }
    public string? SuggestedFix { get; set; }
}
