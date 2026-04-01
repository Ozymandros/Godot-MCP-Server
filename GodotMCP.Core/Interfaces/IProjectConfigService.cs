namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Service for reading and writing simple project-scoped configuration stored in <c>project.godot</c>.
/// </summary>
public interface IProjectConfigService
{
    /// <summary>Get a configuration value from the given section and key. Returns empty string when missing.</summary>
    Task<string> GetValueAsync(string section, string key, CancellationToken cancellationToken = default);

    /// <summary>Set or overwrite a configuration value in the given section and key.</summary>
    Task SetValueAsync(string section, string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Remove a configuration key from the given section if present.</summary>
    Task RemoveKeyAsync(string section, string key, CancellationToken cancellationToken = default);
}
