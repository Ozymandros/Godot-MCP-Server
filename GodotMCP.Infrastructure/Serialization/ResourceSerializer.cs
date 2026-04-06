using System.Text;
using System.Text.RegularExpressions;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Serialization;

/// <summary>
/// Parses and emits simple Godot resource text payloads.
/// </summary>
public sealed class ResourceSerializer : IResourceSerializer
{
    private static readonly Regex ResourceTypeRegex = new("\\[gd_resource\\s+type=\"(?<type>[^\"]+)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <inheritdoc />
    public ResourceDocument DeserializeDocument(string content)
    {
        var typeMatch = ResourceTypeRegex.Match(content);
        var type = typeMatch.Success ? typeMatch.Groups["type"].Value : "Resource";
        var properties = Deserialize(content);
        return new ResourceDocument(type, properties);
    }

    /// <inheritdoc />
    public Dictionary<string, string> Deserialize(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in content.Split(['\n'], StringSplitOptions.None))
        {
            var line = raw.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('['))
            {
                continue;
            }

            var idx = line.IndexOf('=');
            if (idx < 0)
            {
                continue;
            }

            result[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }

        return result;
    }

    /// <inheritdoc />
    public string Serialize(ResourceDocument document)
    {
        return Serialize(document.Type, document.Properties);
    }

    /// <inheritdoc />
    public string Serialize(string type, Dictionary<string, string> properties)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[gd_resource type=\"{type}\" format=3]");
        sb.AppendLine();
        foreach (var pair in properties)
        {
            sb.AppendLine($"{pair.Key} = {pair.Value}");
        }

        return sb.ToString();
    }
}
