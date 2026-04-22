using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using GodotMCP.Core.Interfaces;
using ModelContextProtocol.Server;
using GodotMCP.Core.Models;
using System.IO;

namespace GodotMCP.Application.Tools;

public static partial class GodotTools
{
    /// <summary>
    /// Get file info including content and basic metadata.
    /// </summary>
    /// <param name="fileService">File abstraction for project I/O.</param>
    /// <param name="pathResolver">Project path resolver.</param>
    /// <param name="projectPath">Project directory (absolute path or path relative to the configured project root).</param>
    /// <param name="fileName">File name or relative path under projectPath.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool result containing path, relative path, size, last modified UTC, and file content.</returns>
    [McpServerTool(Name = "get_file_info"), Description("Get file info including content and basic metadata.")]
    public static async Task<ToolResult> GetFileInfoAsync(
        IGodotFileService fileService,
        IPathResolver pathResolver,
        [Description("Project directory (absolute path or path relative to the configured project root)."), Required] string projectPath,
        [Description("File name or relative path under projectPath."), Required] string fileName,
        CancellationToken cancellationToken = default)
    {
        string path;
        try
        {
            path = ResolveProjectFilePath(pathResolver, projectPath, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return Invalid(ex.Message);
        }

        if (!fileService.Exists(path))
        {
            return new ToolResult(false, $"File '{fileName}' not found.");
        }

        string content;
        try
        {
            content = await fileService.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new ToolResult(false, $"Failed to read file: {ex.Message}");
        }

        FileInfo? fi = null;
        try
        {
            fi = new FileInfo(path);
        }
        catch
        {
            fi = null;
        }

        var dto = new
        {
            Path = path,
            RelativePath = ToProjectFileName(path, pathResolver),
            Size = fi?.Exists == true ? fi.Length : 0,
            LastModifiedUtc = fi?.Exists == true ? fi.LastWriteTimeUtc : (DateTime?)null,
            Content = content
        };

        return new ToolResult(true, $"File info for '{fileName}'.", dto);
    }
}
