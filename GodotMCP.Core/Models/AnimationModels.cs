using System.ComponentModel.DataAnnotations;

namespace GodotMCP.Core.Models;

/// <summary>
/// Represents an animation key point used when creating value tracks.
/// </summary>
public sealed class KeyPoint
{
    /// <summary>
    /// Gets or sets key time in seconds.
    /// </summary>
    [Required]
    public float Time { get; set; }

    /// <summary>
    /// Gets or sets serialized Godot value expression.
    /// </summary>
    [Required]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets interpolation transition value.
    /// </summary>
    [Required]
    public float Transition { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets update mode (0=continuous, 1=discrete, 2=trigger).
    /// </summary>
    [Required]
    public int Update { get; set; } = 0;
}

/// <summary>
/// Represents a track payload with target path, type, and key points.
/// </summary>
public sealed class TrackPointModel
{
    /// <summary>
    /// Gets or sets target NodePath expression.
    /// </summary>
    [Required]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets track type token.
    /// </summary>
    [Required]
    public string Type { get; set; } = "value";

    /// <summary>
    /// Gets or sets ordered key points.
    /// </summary>
    [Required, MinLength(1)]
    public List<KeyPoint> KeyPoints { get; set; } = [];
}
