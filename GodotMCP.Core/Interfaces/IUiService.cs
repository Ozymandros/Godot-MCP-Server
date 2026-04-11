using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Provides UI-centric scene operations for control listing and mutation.
/// </summary>
public interface IUiService
{
    /// <summary>
    /// Lists control nodes for a scene.
    /// </summary>
    /// <param name="scenePath">Scene path to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Control node snapshots in deterministic order.</returns>
    Task<IReadOnlyList<UiControlInfo>> ListControlsAsync(string scenePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a control node under a target parent.
    /// </summary>
    /// <param name="request">Control creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result including optional control snapshot.</returns>
    Task<UiMutationResult> AddControlAsync(UiAddControlRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a named layout preset to a control.
    /// </summary>
    /// <param name="request">Layout update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result including optional control snapshot.</returns>
    Task<UiMutationResult> SetLayoutPresetAsync(UiSetLayoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates selected control properties.
    /// </summary>
    /// <param name="request">Property update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result including optional control snapshot.</returns>
    Task<UiMutationResult> SetPropertiesAsync(UiSetPropertiesRequest request, CancellationToken cancellationToken = default);
}
