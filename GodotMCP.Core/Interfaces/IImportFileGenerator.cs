using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Generates serialized Godot import configuration content.
/// </summary>
public interface IImportFileGenerator
{
    /// <summary>
    /// Generates import file text for a model.
    /// </summary>
    /// <param name="model">Import generation model.</param>
    /// <returns>Serialized import file content.</returns>
    string Generate(ImportFileModel model);
}
