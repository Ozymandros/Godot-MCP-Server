using System;
using System.Collections.Generic;
using Xunit;
using GodotMCP.Core.Models;
using GodotMCP.Infrastructure.Serialization;
using Xunit;

namespace GodotMCP.Tests.Unit;

public class ImportAndResourceTests
{
    /// <summary>
    /// Unit tests for import file generation and resource serializer behavior.
    /// </summary>
    [Fact]
    public void ImportGenerator_ShouldRenderExpectedSections()
    {
        var generator = new ImportFileGenerator();
        var model = new ImportFileModel
        {
            AssetPath = "res://assets/a.png",
            Importer = "texture",
            Type = "Texture2D"
        };
        model.Parameters["compress/mode"] = "\"lossy\"";

        var text = generator.Generate(model);
        Assert.Contains("[remap]", text);
        Assert.Contains("[params]", text);
    }

    [Fact]
    public void ResourceSerializer_ShouldRoundTripBasicProperties()
    {
        var serializer = new ResourceSerializer();
        var data = new Dictionary<string, string> { ["size"] = "Vector2(1,1)" };
        var text = serializer.Serialize("Resource", data);
        var parsed = serializer.Deserialize(text);

        Assert.Equal("Vector2(1,1)", parsed["size"]);
    }

    public static IEnumerable<object[]> ImportCases()
    {
        for (var i = 0; i < 35; i++)
        {
            yield return [$"res://assets/{i}.png"];
        }
    }

    [Theory]
    [MemberData(nameof(ImportCases))]
    public void ImportGenerator_ShouldHandleManyAssetNames(string asset)
    {
        var generator = new ImportFileGenerator();
        var model = new ImportFileModel { AssetPath = asset, Importer = "texture", Type = "Texture2D" };
        var output = generator.Generate(model);
        Assert.Contains(asset, output);
    }
}
