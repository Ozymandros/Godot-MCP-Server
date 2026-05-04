using System.Text.Json;
using System.Linq;
using System.IO;
using GodotMCP.Core;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Checks whether a value is null, empty, or whitespace.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns><see langword="true"/> when value is blank.</returns>
    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Creates a failed <see cref="ToolResult"/> with optional remediation guidance.
    /// </summary>
    /// <param name="message">Error message for the tool response.</param>
    /// <param name="remediation">Optional remediation text.</param>
    /// <returns>Failed tool result payload.</returns>
    private static ToolResult Invalid(string message, string? remediation = null)
        => new(false, message, SuggestedRemediation: remediation);

    /// <summary>
    /// Validates that a path resolves inside the project.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="path">Path to validate.</param>
    /// <returns><see langword="true"/> when path is valid and project-scoped.</returns>
    private static bool IsValidProjectFilePath(IPathResolver pathResolver, string path)
    {
        try
        {
            _ = pathResolver.ResolvePath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Normalizes <paramref name="projectPath"/> to the absolute project directory (or subdirectory) used as the base for <c>fileName</c> parameters.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="projectPath">Project root or folder path (absolute or relative to the configured project root).</param>
    /// <returns>Canonical absolute directory path.</returns>
    private static string NormalizeProjectPath(IPathResolver pathResolver, string projectPath)
    {
        if (IsBlank(projectPath))
        {
            throw new InvalidOperationException("projectPath is required.");
        }

        var trimmed = projectPath.Trim();

        if (ProjectPathSyntax.ContainsUriSchemeAuthority(trimmed))
        {
            throw new InvalidOperationException("Path schemes are not supported. Use absolute or project-relative filesystem paths.");
        }

        // If the caller passed an absolute path, use it as the explicit base.
        if (Path.IsPathRooted(trimmed))
        {
            return Path.GetFullPath(trimmed).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return pathResolver.ResolvePath(trimmed);
    }

    /// <summary>
    /// Resolves <paramref name="fileName"/> under <paramref name="projectPath"/> to an absolute file path.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="projectPath">Project base path.</param>
    /// <param name="fileName">File path relative to <paramref name="projectPath"/>.</param>
    /// <returns>Absolute file path inside the project.</returns>
    private static string ResolveProjectFilePath(IPathResolver pathResolver, string projectPath, string fileName)
    {
        if (IsBlank(fileName))
        {
            throw new InvalidOperationException("fileName is required.");
        }

        var projectPathTrimmed = projectPath.Trim();
        var baseDir = NormalizeProjectPath(pathResolver, projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var trimmedName = ProjectPathSyntax.CollapseDuplicateDirectorySeparators(fileName.Trim());

        if (ProjectPathSyntax.IsUncPath(trimmedName))
        {
            var resolvedFull = Path.GetFullPath(trimmedName);
            var uncBaseFull = Path.GetFullPath(baseDir);
            if (!resolvedFull.StartsWith(uncBaseFull, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(resolvedFull, uncBaseFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("fileName must resolve under projectPath.");
            }

            return resolvedFull;
        }

        var normalizedFileName = ProjectPathSyntax.NormalizeRelativePathTokenForCombine(trimmedName);
        var absolute = ProjectPathSyntax.CombineAvoidingDuplicateSegments(baseDir, normalizedFileName);

        // Ensure the resolved absolute path is located under the requested project base.
        var baseFull = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var absoluteFull = Path.GetFullPath(absolute).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!absoluteFull.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(absoluteFull, baseFull.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("fileName must resolve under projectPath.");
        }

        // Only enforce the server-configured project root when the caller used a projectPath
        // that is relative to the configured root. If the caller provided an absolute
        // projectPath, treat the resolved base as authoritative.
        if (!Path.IsPathRooted(projectPathTrimmed))
        {
            pathResolver.EnsureInsideProject(absolute);
        }

        return absolute;
    }

    /// <summary>
    /// Resolves a scene path using the scene contract: <c>projectPath + /scenes/ + fileName</c>.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="projectPath">Project base path (absolute or project-relative).</param>
    /// <param name="fileName">Scene file name relative to the scenes directory.</param>
    /// <returns>Absolute scene file path inside the project.</returns>
    private static string ResolveSceneFilePath(IPathResolver pathResolver, string projectPath, string fileName)
    {
        if (IsBlank(fileName))
        {
            throw new InvalidOperationException("fileName is required.");
        }

        var trimmedFileName = fileName.Trim();
        if (!trimmedFileName.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("fileName must end with '.tscn'.");
        }

        var baseProjectDirectory = NormalizeProjectPath(pathResolver, projectPath);
        var scenesDirectory = ProjectPathSyntax.CombineAvoidingDuplicateSegments(baseProjectDirectory, "scenes");
        return ResolveProjectFilePath(pathResolver, scenesDirectory, trimmedFileName);
    }

    /// <summary>
    /// Ensures a scene exists and is minimally valid before scene/node operations execute.
    /// </summary>
    /// <param name="fileService">Project file service.</param>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="projectPath">Project base path (absolute or project-relative).</param>
    /// <param name="fileName">Scene file name relative to the scenes directory.</param>
    /// <param name="rootType">Root node type used when bootstrap creation is needed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute scene file path.</returns>
    private static async Task<string> EnsureSceneReadyAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        string projectPath,
        string fileName,
        string rootType,
        CancellationToken cancellationToken = default)
    {
        if (IsBlank(rootType))
        {
            throw new InvalidOperationException("rootType is required.");
        }

        var scenePath = ResolveSceneFilePath(pathResolver, projectPath, fileName);
        if (fileService.Exists(scenePath))
        {
            return scenePath;
        }

        var sceneDirectory = Path.GetDirectoryName(scenePath);
        if (!string.IsNullOrWhiteSpace(sceneDirectory))
        {
            fileService.EnsureDirectory(sceneDirectory);
        }

        var bootstrapContent = $"""
[gd_scene format=3]

[node name="Root" type="{rootType.Trim()}"]
""";
        await fileService.WriteAsync(scenePath, $"{bootstrapContent}\n", cancellationToken).ConfigureAwait(false);

        var sceneExists = fileService.Exists(scenePath);
        var sceneCandidateCount = sceneExists ? 1 : 0;
        if (!sceneExists || sceneCandidateCount != 1)
        {
            throw new InvalidOperationException(
                $"Scene bootstrap failed for '{scenePath}' (sceneExists:{sceneExists.ToString().ToLowerInvariant()}, sceneCandidateCount:{sceneCandidateCount}).");
        }

        return scenePath;
    }

    /// <summary>
    /// Resolves multiple file paths under <paramref name="projectPath"/>.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="projectPath">Project base path.</param>
    /// <param name="fileNames">File paths relative to <paramref name="projectPath"/>.</param>
    /// <returns>Absolute paths for each file.</returns>
    private static List<string> ResolveProjectFilePaths(IPathResolver pathResolver, string projectPath, IReadOnlyList<string> fileNames)
    {
        if (fileNames.Count == 0)
        {
            throw new InvalidOperationException("fileNames must contain at least one entry.");
        }

        return fileNames.Select(fileName => ResolveProjectFilePath(pathResolver, projectPath, fileName)).ToList();
    }

    /// <summary>
    /// Converts an absolute or relative path into a <c>fileName</c> token relative to the project root.
    /// </summary>
    /// <param name="path">Path that is absolute under the project, or project-relative.</param>
    /// <param name="pathResolver">Path resolver used when <paramref name="path"/> is absolute.</param>
    /// <returns>Relative path segments using forward slashes.</returns>
    private static string ToProjectFileName(string path, IPathResolver pathResolver)
    {
        if (IsBlank(path))
        {
            throw new InvalidOperationException("path is required.");
        }

        if (ProjectPathSyntax.ContainsUriSchemeAuthority(path.Trim()))
        {
            throw new InvalidOperationException("Path schemes are not supported. Use absolute or project-relative filesystem paths.");
        }

        var absolute = pathResolver.ResolvePath(path);
        return pathResolver.GetProjectRelativePath(absolute);
    }

    /// <summary>
    /// Picks a bootstrap root node type for a scene that will host 2D/3D physics bodies.
    /// </summary>
    /// <param name="bodyType">The type of the body.</param>
    /// <returns>The root node type for the body.</returns>
    private static string InferRootTypeForPhysicsBody(string bodyType)
    {
        var t = bodyType.Trim();
        return t.EndsWith("2D", StringComparison.Ordinal) ? "Node2D" : "Node3D";
    }

    /// <summary>
    /// Picks a bootstrap root node type for a scene that will host lights.
    /// </summary>
    /// <param name="lightType">The type of the light.</param>
    /// <returns>The root node type for the light.</returns>
    private static string InferRootTypeForLight(string lightType)
    {
        var t = lightType.Trim();
        return string.Equals(t, "PointLight2D", StringComparison.Ordinal) ? "Node2D" : "Node3D";
    }

    /// <summary>
    /// Converts a JSON element into a supported primitive CLR value.
    /// </summary>
    /// <param name="value">JSON value to convert.</param>
    /// <returns>Primitive CLR value when supported; otherwise, <see langword="null"/>.</returns>
    private static object? ToPrimitiveValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var i) => i,
            JsonValueKind.Number when value.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
}
