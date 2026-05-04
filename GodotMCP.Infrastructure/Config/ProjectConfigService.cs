using GodotMCP.Core.Interfaces;
using GodotMCP.Core.ProjectSettings;

namespace GodotMCP.Infrastructure.Config;

/// <summary>
/// Provides filesystem-backed read/write operations for <c>project.godot</c> values.
/// </summary>
/// <param name="pathResolver">Project path resolver.</param>
public sealed class ProjectConfigService(IPathResolver pathResolver) : IProjectConfigService
{
    /// <inheritdoc />
    public async Task<string> GetValueAsync(string section, string key, CancellationToken cancellationToken = default)
    {
        var (lines, _, value) = await ReadAndLocateAsync(section, key, cancellationToken).ConfigureAwait(false);
        _ = lines;
        return value ?? string.Empty;
    }

    /// <inheritdoc />
    public Task SetValueAsync(string section, string key, string value, CancellationToken cancellationToken = default) =>
        ProjectGodotMerger.SetSectionKeyAsync(pathResolver.ProjectRoot, section, key, value, cancellationToken);

    /// <inheritdoc />
    public Task RemoveKeyAsync(string section, string key, CancellationToken cancellationToken = default) =>
        ProjectGodotMerger.RemoveSectionKeyAsync(pathResolver.ProjectRoot, section, key, cancellationToken);

    /// <summary>
    /// Reads project config text and locates a section/key pair.
    /// </summary>
    /// <param name="section">Section to locate.</param>
    /// <param name="key">Key to locate within the section.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple with all lines, section index, and existing key value when present.</returns>
    private async Task<(List<string> lines, int sectionIndex, string? value)> ReadAndLocateAsync(
        string section,
        string key,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(pathResolver.ProjectRoot, "project.godot");
        var text = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : string.Empty;
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
        var sectionHeader = $"[{section}]";
        var sectionIndex = lines.FindIndex(l => l.Trim() == sectionHeader);
        if (sectionIndex < 0)
        {
            return (lines, -1, null);
        }

        for (var i = sectionIndex + 1; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                break;
            }

            if (line.StartsWith($"{key}=", StringComparison.Ordinal))
            {
                return (lines, sectionIndex, line[(key.Length + 1)..]);
            }
        }

        return (lines, sectionIndex, null);
    }
}
