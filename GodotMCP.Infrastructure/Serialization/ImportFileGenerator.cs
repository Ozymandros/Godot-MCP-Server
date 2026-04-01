using System.Text;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Serialization;

/// <summary>
/// Generates the textual contents of Godot .import files from an
/// <see cref="ImportFileModel"/> instance.
/// </summary>
public sealed class ImportFileGenerator : IImportFileGenerator
{
    /// <summary>
    /// Generate the textual contents of a .import file for the given model.
    /// </summary>
    public string Generate(ImportFileModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[remap]");
        sb.AppendLine();
        sb.AppendLine($"importer=\"{model.Importer}\"");
        sb.AppendLine($"type=\"{model.Type}\"");
        sb.AppendLine($"path=\"{model.AssetPath}\"");
        sb.AppendLine();
        sb.AppendLine("[params]");
        foreach (var parameter in model.Parameters)
        {
            sb.AppendLine($"{parameter.Key}={parameter.Value}");
        }

        return sb.ToString();
    }
}
