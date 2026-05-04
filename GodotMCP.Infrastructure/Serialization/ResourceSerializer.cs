using System.Text;
using System.Text.RegularExpressions;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Serialization;

/// <summary>
/// Parses and emits Godot <c>.tres</c> / <c>.res</c> text payloads, including optional ext/sub sections.
/// </summary>
public sealed class ResourceSerializer : IResourceSerializer
{
    private static readonly Regex SectionRegex = new(@"^\[(?<tag>[a-z_]+)\s*(?<attrs>.*)\]$", RegexOptions.Compiled);

    /// <inheritdoc />
    public ResourceDocument DeserializeDocument(string content)
    {
        var mainProperties = new Dictionary<string, string>(StringComparer.Ordinal);
        var externalResources = new List<ExtResource>();
        var subResources = new List<SubResource>();
        var type = "Resource";
        var format = 3;
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
                currentSub = null;
                var tag = section.Groups["tag"].Value;
                var attributes = ParseAttrs(section.Groups["attrs"].Value);
                switch (tag)
                {
                    case "gd_resource":
                        type = Get(attributes, "type", type);
                        format = int.TryParse(Get(attributes, "format", format.ToString()), out var fmt) ? fmt : format;
                        break;
                    case "ext_resource":
                        externalResources.Add(new ExtResource
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
                        subResources.Add(currentSub);
                        break;
                    case "resource":
                        currentSub = null;
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
            if (currentSub is not null)
            {
                currentSub.Properties[key] = value;
            }
            else
            {
                mainProperties[key] = value;
            }
        }

        return new ResourceDocument(type, mainProperties)
        {
            ExternalResources = externalResources,
            SubResources = subResources,
            Format = format
        };
    }

    /// <inheritdoc />
    public Dictionary<string, string> Deserialize(string content)
        => DeserializeDocument(content).Properties;

    /// <inheritdoc />
    public string Serialize(ResourceDocument document)
    {
        var sb = new StringBuilder();
        var loadSteps = Math.Max(1, 1 + document.ExternalResources.Count + document.SubResources.Count);
        sb.AppendLine($"[gd_resource type=\"{document.Type}\" load_steps={loadSteps} format={document.Format}]");
        sb.AppendLine();

        foreach (var ext in document.ExternalResources.OrderBy(x => ParseNumericId(x.Id)).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            sb.AppendLine($"[ext_resource type=\"{ext.Type}\" path=\"{ext.Path}\" id=\"{ext.Id}\"]");
        }

        if (document.ExternalResources.Count > 0)
        {
            sb.AppendLine();
        }

        foreach (var sub in document.SubResources.OrderBy(x => ParseNumericId(x.Id)).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            sb.AppendLine($"[sub_resource type=\"{sub.Type}\" id=\"{sub.Id}\"]");
            foreach (var prop in sub.Properties)
            {
                sb.AppendLine($"{prop.Key} = {prop.Value}");
            }

            sb.AppendLine();
        }

        if (document.Properties.Count > 0 && (document.SubResources.Count > 0 || document.ExternalResources.Count > 0))
        {
            sb.AppendLine("[resource]");
            sb.AppendLine();
        }

        foreach (var pair in document.Properties)
        {
            sb.AppendLine($"{pair.Key} = {pair.Value}");
        }

        return sb.ToString();
    }

    /// <inheritdoc />
    public string Serialize(string type, Dictionary<string, string> properties)
        => Serialize(new ResourceDocument(type, properties));

    private static int ParseNumericId(string id) => int.TryParse(id, out var number) ? number : int.MaxValue;

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

    private static string Get(Dictionary<string, string> attrs, string key, string fallback = "")
        => attrs.TryGetValue(key, out var value) ? value : fallback;
}
