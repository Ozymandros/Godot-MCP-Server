using FluentAssertions;
using GodotMCP.Core.Models;
using GodotMCP.Infrastructure.Serialization;

namespace GodotMCP.Tests.Unit;

public class SceneSerializerTests
{
    [Fact]
    public void SerializeAndDeserialize_ShouldRoundTripNodeData()
    {
        var serializer = new SceneSerializer();
        var scene = new GodotScene();
        scene.ExternalResources.Add(new ExtResource { Id = "1", Path = "res://x.tscn", Type = "PackedScene" });
        scene.Nodes.Add(new GodotNode { Name = "Root", Type = "Node2D" });
        scene.Nodes.Add(new GodotNode { Name = "Player", Type = "CharacterBody2D", Parent = "." });

        var text = serializer.Serialize(scene);
        var parsed = serializer.Deserialize(text);

        parsed.Nodes.Should().HaveCount(2);
        parsed.ExternalResources.Should().ContainSingle(x => x.Id == "1");
    }

    public static IEnumerable<object[]> Cases()
    {
        for (var i = 0; i < 40; i++)
        {
            yield return [i];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Serializer_ShouldHandleManyNodeNames(int index)
    {
        var serializer = new SceneSerializer();
        var scene = new GodotScene();
        scene.Nodes.Add(new GodotNode { Name = $"Node{index}", Type = "Node" });

        var output = serializer.Serialize(scene);
        output.Should().Contain($"Node{index}");
    }

    [Fact]
    public void Deserialize_ShouldHandleQuotedAttributesWithSpaces()
    {
        var serializer = new SceneSerializer();
        const string input = """
[gd_scene load_steps=2 format=3]

[ext_resource type="PackedScene" path="res://my scenes/level one.tscn" id="2"]
[node name="Root Node" type="Node2D"]
position = Vector2(10, 20)
""";

        var parsed = serializer.Deserialize(input);
        parsed.ExternalResources.Should().ContainSingle();
        parsed.ExternalResources[0].Path.Should().Be("res://my scenes/level one.tscn");
        parsed.Nodes.Should().ContainSingle();
        parsed.Nodes[0].Name.Should().Be("Root Node");
    }

    [Fact]
    public void Serialize_ShouldOrderResourcesByNumericId()
    {
        var serializer = new SceneSerializer();
        var scene = new GodotScene();
        scene.ExternalResources.Add(new ExtResource { Id = "10", Path = "res://ten.tscn", Type = "PackedScene" });
        scene.ExternalResources.Add(new ExtResource { Id = "2", Path = "res://two.tscn", Type = "PackedScene" });
        scene.SubResources.Add(new SubResource { Id = "5", Type = "Resource" });
        scene.SubResources.Add(new SubResource { Id = "1", Type = "Resource" });
        scene.Nodes.Add(new GodotNode { Name = "Root", Type = "Node" });

        var output = serializer.Serialize(scene);
        output.IndexOf("id=\"2\"", StringComparison.Ordinal)
            .Should()
            .BeLessThan(output.IndexOf("id=\"10\"", StringComparison.Ordinal));
        output.IndexOf("[sub_resource type=\"Resource\" id=\"1\"]", StringComparison.Ordinal)
            .Should()
            .BeLessThan(output.IndexOf("[sub_resource type=\"Resource\" id=\"5\"]", StringComparison.Ordinal));
    }
}
