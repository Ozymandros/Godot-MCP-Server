using System.ComponentModel.DataAnnotations;

namespace GodotMCP.Core.Models;

public sealed class KeyPoint
{
    [Required]
    public float Time { get; set; }
    [Required]
    public string Value { get; set; } = string.Empty;
    [Required]
    public float Transition { get; set; } = 1.0f;
    [Required]
    public int Update { get; set; } = 0; // 0=continuous, 1=discrete, 2=trigger
}

public sealed class TrackPointModel
{
    [Required]
    public string Path { get; set; } = string.Empty;
    [Required]
    public string Type { get; set; } = "value";
    [Required, MinLength(1)]
    public List<KeyPoint> KeyPoints { get; set; } = [];
}
