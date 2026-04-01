namespace GodotMCP.Core.Interfaces;

public interface IProjectConfigService
{
    Task<string> GetValueAsync(string section, string key, CancellationToken cancellationToken = default);
    Task SetValueAsync(string section, string key, string value, CancellationToken cancellationToken = default);
    Task RemoveKeyAsync(string section, string key, CancellationToken cancellationToken = default);
}
