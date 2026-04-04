namespace GodotMCP.Core.Models;

public sealed class KeyPoint
{
    public float Time { get; set; }
    public string Value { get; set; } = string.Empty;
    public float Transition { get; set; } = 1.0f;
    public int Update { get; set; } = 0; // 0=continuous, 1=discrete, 2=trigger
}

public sealed class TrackPointModel
{
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = "value";
    public List<KeyPoint> KeyPoints { get; set; } = [];
}
