using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

public interface ISceneSerializer
{
    GodotScene Deserialize(string content);
    string Serialize(GodotScene scene);
}
