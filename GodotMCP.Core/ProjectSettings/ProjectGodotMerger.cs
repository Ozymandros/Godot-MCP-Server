namespace GodotMCP.Core.ProjectSettings;

/// <summary>
/// Single-key upserts and removals in <c>project.godot</c> without rewriting unrelated sections.
/// </summary>
public static class ProjectGodotMerger
{
    /// <summary>
    /// Sets or inserts a key within a section; preserves comments and other sections.
    /// </summary>
    /// <param name="projectDirectory">Directory containing <c>project.godot</c>.</param>
    /// <param name="section">Section name without brackets.</param>
    /// <param name="key">Key within the section.</param>
    /// <param name="value">Serialized value (already quoted if needed).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SetSectionKeyAsync(
        string projectDirectory,
        string section,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(projectDirectory, "project.godot");
        var text = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : string.Empty;
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
        var sectionHeader = $"[{section}]";
        var sectionLine = lines.FindIndex(l => l.Trim() == sectionHeader);
        var keyLine = $"{key}={value}";
        if (sectionLine < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrEmpty(lines[^1]))
            {
                lines.Add(string.Empty);
            }

            lines.Add(sectionHeader);
            lines.Add(keyLine);
        }
        else
        {
            var insertAt = sectionLine + 1;
            while (insertAt < lines.Count && !lines[insertAt].StartsWith("[", StringComparison.Ordinal))
            {
                insertAt++;
            }

            var replaced = false;
            for (var i = sectionLine + 1; i < insertAt; i++)
            {
                if (lines[i].TrimStart().StartsWith($"{key}=", StringComparison.Ordinal))
                {
                    lines[i] = keyLine;
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                lines.Insert(insertAt, keyLine);
            }
        }

        await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, lines), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a key from a section when the section exists.
    /// </summary>
    /// <param name="projectDirectory">Directory containing <c>project.godot</c>.</param>
    /// <param name="section">Section name without brackets.</param>
    /// <param name="key">Key within the section.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task RemoveSectionKeyAsync(
        string projectDirectory,
        string section,
        string key,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(projectDirectory, "project.godot");
        var text = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false) : string.Empty;
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
        var sectionHeader = $"[{section}]";
        var sectionLine = lines.FindIndex(l => l.Trim() == sectionHeader);
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

        await File.WriteAllTextAsync(path, string.Join(Environment.NewLine, lines), cancellationToken).ConfigureAwait(false);
    }
}
