namespace GodotMCP.Core.Interfaces;

public interface IResourceSerializer
{
    Dictionary<string, string> Deserialize(string content);
    string Serialize(string type, Dictionary<string, string> properties);
}
