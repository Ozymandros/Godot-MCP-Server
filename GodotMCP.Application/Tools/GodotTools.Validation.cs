using System.Text.Json;
using System.Linq;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    private const string DefaultProjectPath = "res://";
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
    /// Validates that a path can be resolved as a project-scoped <c>res://</c> path.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="path">Path to validate.</param>
    /// <returns><see langword="true"/> when path is valid and project-scoped.</returns>
    private static bool IsValidResPath(IPathResolver pathResolver, string path)
    {
        try
        {
            _ = pathResolver.ResolveResPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Normalizes a provided project path (absolute or <c>res://</c>) to canonical <c>res://</c> form.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="projectPath">Project root path input.</param>
    /// <returns>Canonical project path in <c>res://</c> format.</returns>
    private static string NormalizeProjectPath(IPathResolver pathResolver, string projectPath)
    {
        if (IsBlank(projectPath))
        {
            throw new InvalidOperationException("projectPath is required.");
        }

        if (Path.IsPathRooted(projectPath))
        {
            pathResolver.EnsureInsideProject(projectPath);
            return pathResolver.ToResPath(projectPath);
        }

        var absolute = pathResolver.ResolveResPath(projectPath);
        return pathResolver.ToResPath(absolute);
    }

    /// <summary>
    /// Resolves a project-relative file token under <paramref name="projectPath"/> into canonical <c>res://</c>.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="projectPath">Project base path.</param>
    /// <param name="fileName">File token under the project path.</param>
    /// <returns>Canonical project-scoped <c>res://</c> path.</returns>
    private static string ResolveProjectFilePath(IPathResolver pathResolver, string projectPath, string fileName)
    {
        if (IsBlank(fileName))
        {
            throw new InvalidOperationException("fileName is required.");
        }

        var normalizedProjectPath = NormalizeProjectPath(pathResolver, projectPath).TrimEnd('/');
        var normalizedFileName = fileName.Replace('\\', '/').TrimStart('/');
        var combined = $"{normalizedProjectPath}/{normalizedFileName}";
        var absolute = pathResolver.ResolveResPath(combined);
        return pathResolver.ToResPath(absolute);
    }

    /// <summary>
    /// Resolves multiple project-relative file tokens under <paramref name="projectPath"/>.
    /// </summary>
    /// <param name="pathResolver">Path resolver scoped to the current project.</param>
    /// <param name="projectPath">Project base path.</param>
    /// <param name="fileNames">File tokens under the project path.</param>
    /// <returns>Canonical <c>res://</c> paths for each token.</returns>
    private static List<string> ResolveProjectFilePaths(IPathResolver pathResolver, string projectPath, IReadOnlyList<string> fileNames)
    {
        if (fileNames.Count == 0)
        {
            throw new InvalidOperationException("fileNames must contain at least one entry.");
        }

        return fileNames.Select(fileName => ResolveProjectFilePath(pathResolver, projectPath, fileName)).ToList();
    }

    /// <summary>
    /// Converts a legacy <c>res://</c> or project-relative path into a file token under the default project path.
    /// </summary>
    /// <param name="path">Legacy path input.</param>
    /// <returns>File token under <c>res://</c>.</returns>
    private static string ToProjectFileName(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("res://", StringComparison.Ordinal)
            ? normalized["res://".Length..]
            : normalized.TrimStart('/');
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
