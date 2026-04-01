using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

/// <summary>
/// Generates .import file contents for assets. Implementations serialize an <see cref="ImportFileModel"/>
/// into the textual format expected by Godot's importer.
/// </summary>
public interface IImportFileGenerator
{
    /// <summary>
    /// Generates the contents of a .import file for the given <paramref name="model"/>.
    /// </summary>
    /// <param name="model">Import file model describing the asset and importer settings.</param>
    /// <returns>Serialized .import file contents.</returns>
    string Generate(ImportFileModel model);
}
