namespace GodotMCP.Core.Models;

/// <summary>
/// Describes the type or installation profile of an integration/plugin.
/// Used to determine how the integration should be handled by installer tooling.
/// </summary>
public enum IntegrationProfile
{
    /// <summary>
    /// Feature provided by the official engine (first-party) and expected to be
    /// integrated with engine-level support.
    /// </summary>
    OfficialEngineFeature,

    /// <summary>
    /// Community-provided SDK or plugin distributed by the community.
    /// </summary>
    CommunitySdk,

    /// <summary>
    /// Vendor-supplied SDK or plugin distributed by a third-party vendor.
    /// </summary>
    VendorSdk,

    /// <summary>
    /// A project-local plugin that lives inside the project's addons folder and
    /// is specific to the current project.
    /// </summary>
    ProjectLocalPlugin
}
