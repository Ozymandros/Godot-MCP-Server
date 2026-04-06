using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Serializes and deserializes Godot resource property payloads.
/// </summary>
public interface IResourceSerializer
{
    /// <summary>
    /// Parses serialized resource content into a typed resource document model.
    /// </summary>
    /// <param name="content">Serialized resource text.</param>
    /// <returns>Typed resource document.</returns>
    ResourceDocument DeserializeDocument(string content);

    /// <summary>
    /// Parses serialized resource content into key-value properties.
    /// </summary>
    /// <param name="content">Serialized resource text.</param>
    /// <returns>Property dictionary.</returns>
    Dictionary<string, string> Deserialize(string content);

    /// <summary>
    /// Serializes a typed resource document into text.
    /// </summary>
    /// <param name="document">Resource document payload.</param>
    /// <returns>Serialized resource text.</returns>
    string Serialize(ResourceDocument document);

    /// <summary>
    /// Serializes a resource type and property map into text.
    /// </summary>
    /// <param name="type">Godot resource type.</param>
    /// <param name="properties">Resource properties.</param>
    /// <returns>Serialized resource text.</returns>
    string Serialize(string type, Dictionary<string, string> properties);
}
