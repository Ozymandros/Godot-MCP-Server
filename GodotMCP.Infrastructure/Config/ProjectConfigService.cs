using GodotMCP.Core.Interfaces;

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
    public async Task SetValueAsync(string section, string key, string value, CancellationToken cancellationToken = default)
    {
        var (lines, sectionLine, existingValue) = await ReadAndLocateAsync(section, key, cancellationToken).ConfigureAwait(false);
        var keyLine = $"{key}={value}";
        if (existingValue is null)
        {
            if (sectionLine < 0)
            {
                lines.Add(string.Empty);
                lines.Add($"[{section}]");
                lines.Add(keyLine);
            }
            else
            {
                var insertAt = sectionLine + 1;
                while (insertAt < lines.Count && !lines[insertAt].StartsWith("[", StringComparison.Ordinal))
                {
                    insertAt++;
                }

                lines.Insert(insertAt, keyLine);
            }
        }
        else
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].TrimStart().StartsWith($"{key}=", StringComparison.Ordinal))
                {
                    lines[i] = keyLine;
                    break;
                }
            }
        }

        var path = Path.Combine(pathResolver.ProjectRoot, "project.godot");
        await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, lines), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveKeyAsync(string section, string key, CancellationToken cancellationToken = default)
    {
        var (lines, sectionLine, _) = await ReadAndLocateAsync(section, key, cancellationToken).ConfigureAwait(false);
        if (sectionLine < 0)
        {
            return;
        }

        var start = sectionLine + 1;
        var end = lines.Count;
        for (var i = start; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("[", StringComparison.Ordinal))
            {
                end = i;
                break;
            }
        }

        for (var i = start; i < end; i++)
        {
            if (lines[i].TrimStart().StartsWith($"{key}=", StringComparison.Ordinal))
            {
                lines.RemoveAt(i);
                break;
            }
        }

        var path = Path.Combine(pathResolver.ProjectRoot, "project.godot");
        await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, lines), cancellationToken).ConfigureAwait(false);
    }

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
