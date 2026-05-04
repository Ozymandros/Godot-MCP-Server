using System.Globalization;

namespace GodotMCP.Core.ProjectSettings;

/// <summary>
/// Inserts <c>[input]</c> action entries in a <c>project.godot</c> text file.
/// </summary>
public static class ProjectInputMapEditor
{
    /// <summary>
    /// Tries to append an input action with one <c>InputEventKey</c> (physical key code).
    /// Fails when an <c>[input]</c> section already contains actions to avoid corrupting multiline blocks.
    /// </summary>
    public static bool TryAppendPhysicalKeyAction(
        string projectGodotText,
        string actionName,
        int physicalKeyCode,
        out string updatedText,
        out string message)
    {
        updatedText = projectGodotText;
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(actionName))
        {
            message = "actionName is required.";
            return false;
        }

        var keyPrefix = $"{actionName}=";
        if (projectGodotText.Contains(keyPrefix, StringComparison.Ordinal))
        {
            message = $"Action '{actionName}' already exists.";
            return false;
        }

        var lines = projectGodotText.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
        var inputIdx = lines.FindIndex(l => l.Trim() == "[input]");
        if (inputIdx < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrEmpty(lines[^1]))
            {
                lines.Add(string.Empty);
            }

            lines.Add("[input]");
            lines.Add(string.Empty);
            inputIdx = lines.Count - 2;
        }

        var insertAt = FindInsertIndexInInputSection(lines, inputIdx);
        if (insertAt < 0)
        {
            message = "[input] section already contains entries; edit project.godot manually or use write_file for complex merges.";
            return false;
        }

        var inner = BuildPhysicalKeyBlock(physicalKeyCode);
        var blockLines = new List<string> { $"{actionName}={{" };
        foreach (var part in inner.Split(['\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            blockLines.Add(part);
        }

        blockLines.Add("}");
        blockLines.Add(string.Empty);
        lines.InsertRange(insertAt, blockLines);
        updatedText = string.Join(Environment.NewLine, lines);
        message = $"Added input action '{actionName}'.";
        return true;
    }

/// <summary>
/// Finds the index to insert a new input action in the [input] section.
/// </summary>
/// <param name="lines">The lines of the project.godot file.</param>
/// <param name="inputIdx">The index of the [input] section.</param>
/// <returns>The index to insert the new input action.</returns>
    private static int FindInsertIndexInInputSection(List<string> lines, int inputIdx)
    {
        var i = inputIdx + 1;
        for (; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (t.Length == 0)
            {
                continue;
            }

            if (t.StartsWith('['))
            {
                return i;
            }

            return -1;
        }

        return i;
    }

    /// <summary>
    /// Builds the block of text for a physical key action.
    /// </summary>
    /// <param name="physicalKeyCode">The physical key code.</param>
    /// <returns>The block of text for the physical key action.</returns>
    private static string BuildPhysicalKeyBlock(int physicalKeyCode)
    {
        var p = physicalKeyCode.ToString(CultureInfo.InvariantCulture);
        return $"""
"deadzone": 0.5,
"events": [Object(InputEventKey,"resource_local_to_scene":false,"device":-1,"window_id":0,"alt_pressed":false,"shift_pressed":false,"ctrl_pressed":false,"meta_pressed":false,"pressed":false,"keycode":0,"physical_keycode":{p},"key_label":0,"unicode":0,"location":0,"echo":false,"script":null)
]
""";
    }
}
