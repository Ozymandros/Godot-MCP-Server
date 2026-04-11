using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Serializes and deserializes Godot scene files.
/// </summary>
public interface ISceneSerializer
{
    /// <summary>
    /// Parses scene text into a domain scene model.
    /// </summary>
    /// <param name="content">Serialized scene text.</param>
    /// <returns>Parsed scene model.</returns>
    GodotScene Deserialize(string content);

    /// <summary>
    /// Serializes a domain scene model into scene text.
    /// </summary>
    /// <param name="scene">Scene model to serialize.</param>
    /// <returns>Serialized scene text.</returns>
    string Serialize(GodotScene scene);
}
