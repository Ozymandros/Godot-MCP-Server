using System.Text.Json;
using GodotMCP.Application.Tools;

namespace GodotMCP.Tests.Integration;

public class InputMapToolsTests
{
    [Fact]
    public async Task InputMapCrudTools_ShouldRoundTripActionsAndEvents()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            await GodotTools.CreateGodotProjectAsync(files, resolver, root, "Demo");

            (await GodotTools.ProjectInputAddActionAsync(resolver, root, "jump", 0.5)).Success.Should().BeTrue();
            using var payload = JsonDocument.Parse("""{"physical_key_code": 32}""");
            var keyPayload = payload.RootElement.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone(), StringComparer.Ordinal);
            (await GodotTools.ProjectInputAddEventAsync(resolver, root, "jump", "key", keyPayload)).Success.Should().BeTrue();
            (await GodotTools.ProjectInputUpdateActionAsync(resolver, root, "jump", 0.8)).Success.Should().BeTrue();
            (await GodotTools.ProjectInputRemoveEventAsync(resolver, root, "jump", "key", keyPayload)).Success.Should().BeTrue();
            (await GodotTools.ProjectInputRemoveActionAsync(resolver, root, "jump")).Success.Should().BeTrue();

            var list = await GodotTools.ProjectInputListActionsAsync(resolver, root);
            list.Success.Should().BeTrue();
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}

