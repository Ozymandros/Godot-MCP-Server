namespace GodotMCP.Core.Models;

/// <summary>
/// Categorizes integration sources and ownership models.
/// </summary>
public enum IntegrationProfile
{
    /// <summary>
    /// Built-in engine capability.
    /// </summary>
    OfficialEngineFeature,

    /// <summary>
    /// Community-maintained SDK or plugin.
    /// </summary>
    CommunitySdk,

    /// <summary>
    /// Vendor-maintained SDK.
    /// </summary>
    VendorSdk,

    /// <summary>
    /// Project-local custom addon.
    /// </summary>
    ProjectLocalPlugin
}
