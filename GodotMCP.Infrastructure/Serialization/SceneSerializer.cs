using System.Text;
using System.Text.RegularExpressions;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Serialization;

/// <summary>
/// Parses and serializes Godot scene files.
/// </summary>
public sealed class SceneSerializer : ISceneSerializer
{
    /// <summary>
    /// Matches section headers in serialized scene text.
    /// </summary>
    private static readonly Regex SectionRegex = new(@"^\[(?<tag>[a-z_]+)\s*(?<attrs>.*)\]$", RegexOptions.Compiled);

    /// <inheritdoc />
    public GodotScene Deserialize(string content)
    {
        var scene = new GodotScene();
        GodotNode? currentNode = null;
        SubResource? currentSub = null;

        foreach (var rawLine in content.Split(['\n'], StringSplitOptions.None))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var section = SectionRegex.Match(line);
            if (section.Success)
            {
                currentNode = null;
                currentSub = null;
                var tag = section.Groups["tag"].Value;
                var attributes = ParseAttrs(section.Groups["attrs"].Value);
                switch (tag)
                {
                    case "gd_scene":
                        scene.LoadSteps = int.TryParse(Get(attributes, "load_steps", "1"), out var steps) ? steps : 1;
                        scene.Format = int.TryParse(Get(attributes, "format", "3"), out var format) ? format : 3;
                        break;
                    case "ext_resource":
                        scene.ExternalResources.Add(new ExtResource
                        {
                            Id = Get(attributes, "id"),
                            Type = Get(attributes, "type"),
                            Path = Get(attributes, "path")
                        });
                        break;
                    case "sub_resource":
                        currentSub = new SubResource
                        {
                            Id = Get(attributes, "id"),
                            Type = Get(attributes, "type")
                        };
                        scene.SubResources.Add(currentSub);
                        break;
                    case "node":
                        currentNode = new GodotNode
                        {
                            Name = Get(attributes, "name"),
                            Type = Get(attributes, "type"),
                            Parent = Get(attributes, "parent", string.Empty),
                            Instance = attributes.GetValueOrDefault("instance")
                        };
                        scene.Nodes.Add(currentNode);
                        break;
                }

                continue;
            }

            var eqIndex = line.IndexOf('=');
            if (eqIndex < 0)
            {
                continue;
            }

            var key = line[..eqIndex].Trim();
            var value = line[(eqIndex + 1)..].Trim();
            if (currentNode is not null)
            {
                currentNode.Properties[key] = value;
            }
            else if (currentSub is not null)
            {
                currentSub.Properties[key] = value;
            }
        }

        return scene;
    }

    /// <inheritdoc />
    public string Serialize(GodotScene scene)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[gd_scene load_steps={scene.LoadSteps} format={scene.Format}]");
        sb.AppendLine();

        foreach (var ext in scene.ExternalResources.OrderBy(x => ParseNumericId(x.Id)).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            sb.AppendLine($"[ext_resource type=\"{ext.Type}\" path=\"{ext.Path}\" id=\"{ext.Id}\"]");
        }

        if (scene.ExternalResources.Count > 0)
        {
            sb.AppendLine();
        }

        foreach (var sub in scene.SubResources.OrderBy(x => ParseNumericId(x.Id)).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            sb.AppendLine($"[sub_resource type=\"{sub.Type}\" id=\"{sub.Id}\"]");
            foreach (var prop in sub.Properties)
            {
                sb.AppendLine($"{prop.Key} = {prop.Value}");
            }

            sb.AppendLine();
        }

        foreach (var node in scene.Nodes)
        {
            var parent = string.IsNullOrWhiteSpace(node.Parent) ? string.Empty : $" parent=\"{node.Parent}\"";
            var instance = string.IsNullOrWhiteSpace(node.Instance) ? string.Empty : $" instance={node.Instance}";
            sb.AppendLine($"[node name=\"{node.Name}\" type=\"{node.Type}\"{parent}{instance}]");
            foreach (var prop in node.Properties)
            {
                sb.AppendLine($"{prop.Key} = {prop.Value}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Parses section attributes from a scene header line.
    /// </summary>
    /// <param name="attrs">Raw attribute text.</param>
    /// <returns>Parsed key-value attributes.</returns>
    private static Dictionary<string, string> ParseAttrs(string attrs)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        var i = 0;
        while (i < attrs.Length)
        {
            while (i < attrs.Length && char.IsWhiteSpace(attrs[i]))
            {
                i++;
            }

            if (i >= attrs.Length)
            {
                break;
            }

            var keyStart = i;
            while (i < attrs.Length && attrs[i] != '=' && !char.IsWhiteSpace(attrs[i]))
            {
                i++;
            }

            if (i >= attrs.Length || attrs[i] != '=')
            {
                while (i < attrs.Length && !char.IsWhiteSpace(attrs[i]))
                {
                    i++;
                }

                continue;
            }

            var key = attrs[keyStart..i];
            i++;
            if (i >= attrs.Length)
            {
                dict[key] = string.Empty;
                break;
            }

            string value;
            if (attrs[i] == '"')
            {
                i++;
                var valueStart = i;
                while (i < attrs.Length && attrs[i] != '"')
                {
                    i++;
                }

                value = attrs[valueStart..Math.Min(i, attrs.Length)];
                if (i < attrs.Length && attrs[i] == '"')
                {
                    i++;
                }
            }
            else
            {
                var valueStart = i;
                while (i < attrs.Length && !char.IsWhiteSpace(attrs[i]))
                {
                    i++;
                }

                value = attrs[valueStart..i];
            }

            dict[key] = value;
        }

        return dict;
    }

    /// <summary>
    /// Parses numeric identifiers for deterministic ordering.
    /// </summary>
    /// <param name="id">Identifier text.</param>
    /// <returns>Numeric value or <see cref="int.MaxValue"/> when non-numeric.</returns>
    private static int ParseNumericId(string id) => int.TryParse(id, out var number) ? number : int.MaxValue;

    /// <summary>
    /// Gets an attribute value with fallback behavior.
    /// </summary>
    /// <param name="attrs">Attribute dictionary.</param>
    /// <param name="key">Key to read.</param>
    /// <param name="fallback">Fallback value when key is missing.</param>
    /// <returns>Resolved attribute value.</returns>
    private static string Get(Dictionary<string, string> attrs, string key, string fallback = "")
        => attrs.TryGetValue(key, out var value) ? value : fallback;
}
