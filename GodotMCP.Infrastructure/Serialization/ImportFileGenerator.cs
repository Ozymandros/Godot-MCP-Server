using System.Text;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;

namespace GodotMCP.Infrastructure.Serialization;

public sealed class ImportFileGenerator : IImportFileGenerator
{
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
