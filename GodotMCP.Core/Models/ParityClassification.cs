namespace GodotMCP.Core.Models;

/// <summary>
/// Classifies parity relationships between Unity and Godot capabilities.
/// </summary>
public enum ParityClassification
{
    /// <summary>
    /// Capability maps directly across engines.
    /// </summary>
    DirectEquivalent,

    /// <summary>
    /// Capability exists as a native alternative pattern.
    /// </summary>
    NativeAlternative,

    /// <summary>
    /// Capability has no current equivalent and is deferred.
    /// </summary>
    DeferredNoEquivalent
}
