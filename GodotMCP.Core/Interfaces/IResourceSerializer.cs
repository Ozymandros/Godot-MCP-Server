namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Serializes simple Godot resource property bags and parses them back into
/// key/value dictionaries.
/// </summary>
public interface IResourceSerializer
{
    /// <summary>
    /// Parse resource content into a dictionary of properties.
    /// </summary>
    Dictionary<string, string> Deserialize(string content);

    /// <summary>
    /// Render a resource property dictionary into textual content for a given
    /// resource type.
    /// </summary>
    string Serialize(string type, Dictionary<string, string> properties);
}
