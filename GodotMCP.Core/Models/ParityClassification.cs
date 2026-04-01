namespace GodotMCP.Core.Models;

/// <summary>
/// Classification used to describe how a feature or API maps between engines.
/// </summary>
public enum ParityClassification
{
    /// <summary>Feature has a direct equivalent in the target engine.</summary>
    DirectEquivalent,
    /// <summary>Feature can be achieved via a native alternative in the target engine.</summary>
    NativeAlternative,
    /// <summary>No equivalent exists; the feature is deferred or not supported.</summary>
    DeferredNoEquivalent
}
