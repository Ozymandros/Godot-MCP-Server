using System.Text.Json;
using System.Linq;
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

        if (ProjectPathSyntax.ContainsUriSchemeAuthority(projectPath.Trim()))
        {
            throw new InvalidOperationException("Path schemes are not supported. Use absolute or project-relative filesystem paths.");
        }

        return pathResolver.ResolvePath(projectPath);
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

        var baseDir = NormalizeProjectPath(pathResolver, projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var trimmedName = ProjectPathSyntax.CollapseDuplicateDirectorySeparators(fileName.Trim());

        if (ProjectPathSyntax.IsUncPath(trimmedName))
        {
            var resolved = pathResolver.ResolvePath(trimmedName);
            var baseFull = Path.GetFullPath(baseDir);
            var resolvedFull = Path.GetFullPath(resolved);
            if (!resolvedFull.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(resolvedFull, baseFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("fileName must resolve under projectPath.");
            }

            return resolvedFull;
        }

        var normalizedFileName = ProjectPathSyntax.NormalizeRelativePathTokenForCombine(trimmedName);
        var absolute = ProjectPathSyntax.CombineAvoidingDuplicateSegments(baseDir, normalizedFileName);
        pathResolver.EnsureInsideProject(absolute);
        return absolute;
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
