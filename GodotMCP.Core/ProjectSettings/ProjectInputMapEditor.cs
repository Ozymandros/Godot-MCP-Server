using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GodotMCP.Core.ProjectSettings;

/// <summary>
/// Edits <c>[input]</c> action entries in a <c>project.godot</c> text file.
/// </summary>
public static class ProjectInputMapEditor
{
    public static bool TryAppendPhysicalKeyAction(
        string projectGodotText,
        string actionName,
        int physicalKeyCode,
        out string updatedText,
        out string message)
    {
        updatedText = projectGodotText;
        message = string.Empty;
        var key = ProjectInputEvent.Key(physicalKeyCode: physicalKeyCode);

        if (!TryAddAction(projectGodotText, actionName, 0.5, overwriteIfExists: false, out var afterAction, out message))
        {
            return false;
        }

        if (!TryAddEvent(afterAction, actionName, key, allowDuplicate: false, out updatedText, out message))
        {
            return false;
        }

        message = $"Added input action '{actionName}'.";
        return true;
    }

    public static bool TryListActions(string projectGodotText, out IReadOnlyList<ProjectInputAction> actions, out string message)
    {
        if (!TryParse(projectGodotText, out var doc, out message))
        {
            actions = [];
            return false;
        }

        actions = doc.Actions.Values.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
        return true;
    }

    public static bool TryAddAction(string projectGodotText, string actionName, double deadzone, bool overwriteIfExists, out string updatedText, out string message)
    {
        updatedText = projectGodotText;
        if (!TryParse(projectGodotText, out var doc, out message))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(actionName))
        {
            message = "actionName is required.";
            return false;
        }

        var normalized = actionName.Trim();
        if (doc.Actions.TryGetValue(normalized, out var existing))
        {
            if (!overwriteIfExists)
            {
                message = $"Action '{normalized}' already exists.";
                return false;
            }

            doc.Actions[normalized] = existing with { Deadzone = deadzone };
        }
        else
        {
            doc.Actions[normalized] = new ProjectInputAction(normalized, deadzone, []);
        }

        updatedText = SerializeDocument(doc);
        message = $"Action '{normalized}' saved.";
        return true;
    }

    public static bool TryRemoveAction(string projectGodotText, string actionName, out string updatedText, out string message)
    {
        updatedText = projectGodotText;
        if (!TryParse(projectGodotText, out var doc, out message))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(actionName))
        {
            message = "actionName is required.";
            return false;
        }

        var normalized = actionName.Trim();
        if (!doc.Actions.Remove(normalized))
        {
            message = $"Action '{normalized}' was not found.";
            return false;
        }

        updatedText = SerializeDocument(doc);
        message = $"Action '{normalized}' removed.";
        return true;
    }

    public static bool TryUpdateDeadzone(string projectGodotText, string actionName, double deadzone, out string updatedText, out string message)
    {
        updatedText = projectGodotText;
        if (!TryParse(projectGodotText, out var doc, out message))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(actionName))
        {
            message = "actionName is required.";
            return false;
        }

        var normalized = actionName.Trim();
        if (!doc.Actions.TryGetValue(normalized, out var existing))
        {
            message = $"Action '{normalized}' was not found.";
            return false;
        }

        doc.Actions[normalized] = existing with { Deadzone = deadzone };
        updatedText = SerializeDocument(doc);
        message = $"Updated deadzone for '{normalized}'.";
        return true;
    }

    public static bool TryAddEvent(string projectGodotText, string actionName, ProjectInputEvent inputEvent, bool allowDuplicate, out string updatedText, out string message)
    {
        updatedText = projectGodotText;
        if (!TryParse(projectGodotText, out var doc, out message))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(actionName))
        {
            message = "actionName is required.";
            return false;
        }

        var normalized = actionName.Trim();
        if (!doc.Actions.TryGetValue(normalized, out var existing))
        {
            message = $"Action '{normalized}' was not found.";
            return false;
        }

        if (!allowDuplicate && existing.Events.Any(e => e.CanonicalKey == inputEvent.CanonicalKey))
        {
            message = $"Event already exists on '{normalized}'.";
            return false;
        }

        var events = existing.Events.ToList();
        events.Add(inputEvent);
        doc.Actions[normalized] = existing with { Events = events };

        updatedText = SerializeDocument(doc);
        message = $"Added event to '{normalized}'.";
        return true;
    }

    public static bool TryRemoveEvent(string projectGodotText, string actionName, ProjectInputEvent inputEvent, out string updatedText, out string message)
    {
        updatedText = projectGodotText;
        if (!TryParse(projectGodotText, out var doc, out message))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(actionName))
        {
            message = "actionName is required.";
            return false;
        }

        var normalized = actionName.Trim();
        if (!doc.Actions.TryGetValue(normalized, out var existing))
        {
            message = $"Action '{normalized}' was not found.";
            return false;
        }

        var events = existing.Events.ToList();
        var index = events.FindIndex(e => e.CanonicalKey == inputEvent.CanonicalKey);
        if (index < 0)
        {
            message = $"Event was not found on '{normalized}'.";
            return false;
        }

        events.RemoveAt(index);
        doc.Actions[normalized] = existing with { Events = events };
        updatedText = SerializeDocument(doc);
        message = $"Removed event from '{normalized}'.";
        return true;
    }

    private static bool TryParse(string text, out ProjectInputDocument document, out string message)
    {
        message = string.Empty;
        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.None).ToList();
        var inputIdx = lines.FindIndex(l => l.Trim() == "[input]");
        var before = new List<string>();
        var after = new List<string>();
        var actions = new Dictionary<string, ProjectInputAction>(StringComparer.Ordinal);

        if (inputIdx < 0)
        {
            before.AddRange(lines);
            document = new ProjectInputDocument(before, actions, after);
            return true;
        }

        before.AddRange(lines.Take(inputIdx));
        var nextSection = lines.Count;
        for (var i = inputIdx + 1; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                nextSection = i;
                break;
            }
        }

        after.AddRange(lines.Skip(nextSection));
        var body = lines.Skip(inputIdx + 1).Take(nextSection - inputIdx - 1).ToList();
        for (var i = 0; i < body.Count;)
        {
            var line = body[i].Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                i++;
                continue;
            }

            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0 || !line.EndsWith("{", StringComparison.Ordinal))
            {
                message = $"Unsupported [input] format near '{line}'.";
                document = default!;
                return false;
            }

            var name = line[..eq].Trim();
            var block = new StringBuilder();
            var depth = 0;
            for (; i < body.Count; i++)
            {
                var current = body[i];
                block.AppendLine(current);
                depth += current.Count(c => c == '{');
                depth -= current.Count(c => c == '}');
                if (depth == 0)
                {
                    i++;
                    break;
                }
            }

            if (depth != 0)
            {
                message = $"Unbalanced input action block for '{name}'.";
                document = default!;
                return false;
            }

            if (!TryParseActionBlock(name, block.ToString(), out var action, out message))
            {
                document = default!;
                return false;
            }

            actions[name] = action;
        }

        document = new ProjectInputDocument(before, actions, after);
        return true;
    }

    private static bool TryParseActionBlock(string actionName, string blockText, out ProjectInputAction action, out string message)
    {
        message = string.Empty;
        var deadzone = 0.5;
        var deadzoneMatch = Regex.Match(blockText, "\"deadzone\"\\s*:\\s*([0-9]+(?:\\.[0-9]+)?)", RegexOptions.CultureInvariant);
        if (deadzoneMatch.Success
            && !double.TryParse(deadzoneMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out deadzone))
        {
            action = default!;
            message = $"Invalid deadzone for '{actionName}'.";
            return false;
        }

        var eventsMatch = Regex.Match(blockText, "\"events\"\\s*:\\s*\\[(.*)\\]", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        var events = new List<ProjectInputEvent>();
        if (eventsMatch.Success)
        {
            foreach (var obj in SplitTopLevelObjects(eventsMatch.Groups[1].Value))
            {
                if (TryParseEventObject(obj, out var parsed))
                {
                    events.Add(parsed);
                }
            }
        }

        action = new ProjectInputAction(actionName, deadzone, events);
        return true;
    }

    private static IEnumerable<string> SplitTopLevelObjects(string arrayBody)
    {
        var start = -1;
        var depth = 0;
        for (var i = 0; i < arrayBody.Length; i++)
        {
            if (arrayBody.AsSpan(i).StartsWith("Object(", StringComparison.Ordinal))
            {
                if (start < 0)
                {
                    start = i;
                }
            }

            var ch = arrayBody[i];
            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')')
            {
                depth--;
                if (depth == 0 && start >= 0)
                {
                    yield return arrayBody[start..(i + 1)];
                    start = -1;
                }
            }
        }
    }

    private static bool TryParseEventObject(string objectText, out ProjectInputEvent evt)
    {
        evt = default!;
        if (!objectText.StartsWith("Object(", StringComparison.Ordinal))
        {
            return false;
        }

        var typeEnd = objectText.IndexOf(',', "Object(".Length);
        if (typeEnd < 0)
        {
            return false;
        }

        var type = objectText["Object(".Length..typeEnd].Trim();
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var fieldsRaw = objectText[(typeEnd + 1)..^1];
        foreach (Match m in Regex.Matches(fieldsRaw, "\"([^\"]+)\"\\s*:\\s*([^,\\)]+)", RegexOptions.CultureInvariant))
        {
            fields[m.Groups[1].Value] = m.Groups[2].Value.Trim();
        }

        evt = ProjectInputEvent.FromSerialized(type, fields);
        return true;
    }

    private static string SerializeDocument(ProjectInputDocument document)
    {
        var lines = new List<string>();
        lines.AddRange(document.BeforeInputSection);
        if (lines.Count > 0 && lines[^1].Length != 0)
        {
            lines.Add(string.Empty);
        }

        lines.Add("[input]");
        foreach (var action in document.Actions.Values.OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            lines.Add($"{action.Name}={{");
            lines.Add($"\"deadzone\": {action.Deadzone.ToString("0.0###", CultureInfo.InvariantCulture)},");
            lines.Add("\"events\": [" + string.Join(",", action.Events.Select(SerializeEventObject)) + "]");
            lines.Add("}");
            lines.Add(string.Empty);
        }

        if (document.AfterInputSection.Count > 0)
        {
            if (lines.Count > 0 && lines[^1].Length != 0)
            {
                lines.Add(string.Empty);
            }

            lines.AddRange(document.AfterInputSection);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string SerializeEventObject(ProjectInputEvent evt)
    {
        var parts = new List<string>();
        foreach (var (key, value) in evt.SerializedFields.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            parts.Add($"\"{key}\":{value}");
        }

        return $"Object({evt.SerializedType},{string.Join(",", parts)})";
    }
}

public sealed record ProjectInputDocument(
    IReadOnlyList<string> BeforeInputSection,
    Dictionary<string, ProjectInputAction> Actions,
    IReadOnlyList<string> AfterInputSection);

public sealed record ProjectInputAction(string Name, double Deadzone, IReadOnlyList<ProjectInputEvent> Events);

public sealed record ProjectInputEvent(string EventType, string CanonicalKey, string SerializedType, IReadOnlyDictionary<string, string> SerializedFields)
{
    public static ProjectInputEvent Key(
        int? physicalKeyCode = null,
        int? keyCode = null,
        bool shift = false,
        bool alt = false,
        bool ctrl = false,
        bool meta = false)
    {
        var fields = Defaults();
        fields["alt_pressed"] = Bool(alt);
        fields["shift_pressed"] = Bool(shift);
        fields["ctrl_pressed"] = Bool(ctrl);
        fields["meta_pressed"] = Bool(meta);
        fields["keycode"] = (keyCode ?? 0).ToString(CultureInfo.InvariantCulture);
        fields["physical_keycode"] = (physicalKeyCode ?? 0).ToString(CultureInfo.InvariantCulture);
        var canonical = $"key:{fields["physical_keycode"]}:{fields["keycode"]}:{fields["shift_pressed"]}:{fields["alt_pressed"]}:{fields["ctrl_pressed"]}:{fields["meta_pressed"]}";
        return new ProjectInputEvent("key", canonical, "InputEventKey", fields);
    }

    public static ProjectInputEvent MouseButton(int buttonIndex, bool doubleClick = false)
    {
        var fields = Defaults();
        fields["button_index"] = buttonIndex.ToString(CultureInfo.InvariantCulture);
        fields["double_click"] = Bool(doubleClick);
        var canonical = $"mouse_button:{buttonIndex}:{fields["double_click"]}";
        return new ProjectInputEvent("mouse_button", canonical, "InputEventMouseButton", fields);
    }

    public static ProjectInputEvent JoypadButton(int buttonIndex)
    {
        var fields = Defaults();
        fields["button_index"] = buttonIndex.ToString(CultureInfo.InvariantCulture);
        fields["pressure"] = "0.0";
        var canonical = $"joypad_button:{buttonIndex}";
        return new ProjectInputEvent("joypad_button", canonical, "InputEventJoypadButton", fields);
    }

    public static ProjectInputEvent JoypadMotion(int axis, double axisValue)
    {
        var fields = Defaults();
        fields["axis"] = axis.ToString(CultureInfo.InvariantCulture);
        fields["axis_value"] = axisValue.ToString("0.0###", CultureInfo.InvariantCulture);
        var canonical = $"joypad_motion:{fields["axis"]}:{fields["axis_value"]}";
        return new ProjectInputEvent("joypad_motion", canonical, "InputEventJoypadMotion", fields);
    }

    public static ProjectInputEvent FromSerialized(string serializedType, IReadOnlyDictionary<string, string> fields)
    {
        var type = serializedType switch
        {
            "InputEventKey" => "key",
            "InputEventMouseButton" => "mouse_button",
            "InputEventJoypadButton" => "joypad_button",
            "InputEventJoypadMotion" => "joypad_motion",
            _ => "other"
        };

        var canonical = type switch
        {
            "key" => $"key:{fields.GetValueOrDefault("physical_keycode", "0")}:{fields.GetValueOrDefault("keycode", "0")}:{fields.GetValueOrDefault("shift_pressed", "false")}:{fields.GetValueOrDefault("alt_pressed", "false")}:{fields.GetValueOrDefault("ctrl_pressed", "false")}:{fields.GetValueOrDefault("meta_pressed", "false")}",
            "mouse_button" => $"mouse_button:{fields.GetValueOrDefault("button_index", "0")}:{fields.GetValueOrDefault("double_click", "false")}",
            "joypad_button" => $"joypad_button:{fields.GetValueOrDefault("button_index", "0")}",
            "joypad_motion" => $"joypad_motion:{fields.GetValueOrDefault("axis", "0")}:{fields.GetValueOrDefault("axis_value", "0")}",
            _ => $"other:{serializedType}"
        };

        return new ProjectInputEvent(type, canonical, serializedType, new Dictionary<string, string>(fields, StringComparer.Ordinal));
    }

    private static Dictionary<string, string> Defaults()
        => new(StringComparer.Ordinal)
        {
            ["resource_local_to_scene"] = "false",
            ["device"] = "-1",
            ["window_id"] = "0",
            ["alt_pressed"] = "false",
            ["shift_pressed"] = "false",
            ["ctrl_pressed"] = "false",
            ["meta_pressed"] = "false",
            ["pressed"] = "false",
            ["keycode"] = "0",
            ["physical_keycode"] = "0",
            ["key_label"] = "0",
            ["unicode"] = "0",
            ["location"] = "0",
            ["echo"] = "false",
            ["script"] = "null"
        };

    private static string Bool(bool v) => v ? "true" : "false";
}
