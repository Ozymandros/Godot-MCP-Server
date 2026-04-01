using System.Text;
using GodotMCP.Core.Interfaces;

namespace GodotMCP.Infrastructure.Serialization;

/// <summary>
/// Serialize and deserialize simple Godot resource (gd_resource) property files.
/// </summary>
public sealed class ResourceSerializer : IResourceSerializer
{
    /// <summary>Deserialize a Godot resource file into a key/value dictionary.</summary>
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

    /// <summary>Serialize a resource properties dictionary into a gd_resource text payload.</summary>
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
