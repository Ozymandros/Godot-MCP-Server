using FluentAssertions;
using GodotMCP.Core.Models;
using GodotMCP.Core.ProjectSettings;
using GodotMCP.Infrastructure.Serialization;

namespace GodotMCP.Tests.Unit;

public class ImportAndResourceTests
{
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
        text.Should().Contain("[remap]");
        text.Should().Contain("[params]");
    }

    [Fact]
    public void ResourceSerializer_ShouldRoundTripBasicProperties()
    {
        var serializer = new ResourceSerializer();
        var data = new Dictionary<string, string> { ["size"] = "Vector2(1,1)" };
        var text = serializer.Serialize("Resource", data);
        var parsed = serializer.Deserialize(text);

        parsed["size"].Should().Be("Vector2(1,1)");
    }

    /// <summary>
    /// Verifies ext_resource / sub_resource sections round-trip.
    /// </summary>
    [Fact]
    public void ResourceSerializer_ShouldRoundTripExtAndSubResources()
    {
        var serializer = new ResourceSerializer();
        var sub = new SubResource { Id = "1", Type = "ShaderMaterial" };
        sub.Properties["shader_parameter/foo"] = "1.0";
        var doc = new ResourceDocument("ShaderMaterial", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["shader"] = "SubResource(\"1\")"
        })
        {
            ExternalResources =
            [
                new ExtResource { Id = "2", Path = "res://tex.png", Type = "Texture2D" }
            ],
            SubResources = [sub]
        };

        var text = serializer.Serialize(doc);
        var round = serializer.DeserializeDocument(text);

        round.ExternalResources.Should().ContainSingle(x => x.Id == "2");
        round.SubResources.Should().ContainSingle(x => x.Id == "1");
        round.Properties["shader"].Should().Contain("SubResource(\"1\")");
    }

    [Fact]
    public void ProjectInputMapEditor_ShouldCreateInputSectionWithKey()
    {
        const string t = "config_version=5\n";
        var ok = ProjectInputMapEditor.TryAppendPhysicalKeyAction(t, "jump", 32, out var updated, out _);
        ok.Should().BeTrue();
        updated.Should().Contain("[input]");
        updated.Should().Contain("jump=");
        updated.Should().Contain("physical_keycode\":32");
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
        output.Should().Contain(asset);
    }
}
