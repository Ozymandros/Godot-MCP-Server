using GodotMCP.Application.Tools;
using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Serialization;
using GodotMCP.Infrastructure.Services;

namespace GodotMCP.Tests.Integration;

public class SignalConnectionToolsTests
{
    [Fact]
    public async Task SceneConnectionTools_ShouldAddListUpdateRemove()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await GodotTools.CreateGodotProjectAsync(files, resolver, root, "Demo");
            var sceneSerializer = new SceneSerializer();
            ISceneGraphService graph = new SceneGraphService(files, sceneSerializer, resolver);

            (await GodotTools.CreateSceneAsync(files, resolver, sceneSerializer, root, "Main.tscn", "Root", "Node2D")).Success.Should().BeTrue();
            (await GodotTools.SceneAddNodeAsync(graph, files, resolver, root, "Main.tscn", ".", "Button", "Button", "Node2D")).Success.Should().BeTrue();
            (await GodotTools.SceneAddNodeAsync(graph, files, resolver, root, "Main.tscn", ".", "Node", "Receiver", "Node2D")).Success.Should().BeTrue();
            await files.WriteAsync(Path.Combine(root, "scripts", "Receiver.gd"), "extends Node\nfunc on_pressed():\n    pass\n");
            (await GodotTools.AttachScriptAsync(files, resolver, sceneSerializer, root, "Main.tscn", "Receiver", "scripts/Receiver.gd")).Success.Should().BeTrue();

            (await GodotTools.SceneAddConnectionAsync(graph, files, resolver, root, "Main.tscn", "pressed", "Button", "Receiver", "on_pressed")).Success.Should().BeTrue();
            var list = await GodotTools.SceneListConnectionsAsync(graph, files, resolver, root, "Main.tscn");
            list.Success.Should().BeTrue();

            (await GodotTools.SceneUpdateConnectionAsync(graph, files, resolver, root, "Main.tscn", "pressed", "Button", "Receiver", "on_pressed", "button_up", "Button", "Receiver", "on_pressed")).Success.Should().BeTrue();
            (await GodotTools.SceneRemoveConnectionAsync(graph, files, resolver, root, "Main.tscn", "button_up", "Button", "Receiver", "on_pressed")).Success.Should().BeTrue();
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}

