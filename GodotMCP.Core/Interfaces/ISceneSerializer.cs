using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Serializes and deserializes Godot scene text format to/from a strongly-typed
/// <see cref="GodotScene"/> model.
/// </summary>
public interface ISceneSerializer
{
    /// <summary>
    /// Deserialize textual scene content into a <see cref="GodotScene"/> model.
    /// </summary>
    GodotScene Deserialize(string content);

    /// <summary>
    /// Serialize a <see cref="GodotScene"/> model into Godot scene text format.
    /// </summary>
    string Serialize(GodotScene scene);
}
