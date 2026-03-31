using System.Text;
using GodotMCP.Core.Interfaces;

namespace GodotMCP.Infrastructure.Serialization;

public sealed class ResourceSerializer : IResourceSerializer
{
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
