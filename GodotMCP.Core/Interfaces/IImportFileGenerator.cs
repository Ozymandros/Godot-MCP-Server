using GodotMCP.Core.Models;

namespace GodotMCP.Core.Interfaces;

public interface IImportFileGenerator
{
    string Generate(ImportFileModel model);
}
