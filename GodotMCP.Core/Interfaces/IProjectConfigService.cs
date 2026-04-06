namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Provides read and write access to <c>project.godot</c> configuration values.
/// </summary>
public interface IProjectConfigService
{
    /// <summary>
    /// Gets a key value from a configuration section.
    /// </summary>
    /// <param name="section">Configuration section name.</param>
    /// <param name="key">Configuration key name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stored value or an empty string when missing.</returns>
    Task<string> GetValueAsync(string section, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets or inserts a key value inside a configuration section.
    /// </summary>
    /// <param name="section">Configuration section name.</param>
    /// <param name="key">Configuration key name.</param>
    /// <param name="value">Value to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetValueAsync(string section, string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a key from a configuration section when present.
    /// </summary>
    /// <param name="section">Configuration section name.</param>
    /// <param name="key">Configuration key name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveKeyAsync(string section, string key, CancellationToken cancellationToken = default);
}
