using FluentAssertions;
using GodotMCP.Core.ProjectSettings;

namespace GodotMCP.Tests.Unit;

public class ProjectInputMapEditorTests
{
    [Fact]
    public void InputEditor_ShouldSupportActionAndEventCrud()
    {
        var text = "config_version=5\n";
        ProjectInputMapEditor.TryAddAction(text, "jump", 0.5, overwriteIfExists: false, out var t1, out _).Should().BeTrue();
        ProjectInputMapEditor.TryAddEvent(t1, "jump", ProjectInputEvent.Key(physicalKeyCode: 32), false, out var t2, out _).Should().BeTrue();
        ProjectInputMapEditor.TryAddEvent(t2, "jump", ProjectInputEvent.MouseButton(1), false, out var t3, out _).Should().BeTrue();
        ProjectInputMapEditor.TryUpdateDeadzone(t3, "jump", 0.7, out var t4, out _).Should().BeTrue();
        ProjectInputMapEditor.TryRemoveEvent(t4, "jump", ProjectInputEvent.MouseButton(1), out var t5, out _).Should().BeTrue();
        ProjectInputMapEditor.TryListActions(t5, out var actions, out _).Should().BeTrue();

        actions.Should().ContainSingle(a => a.Name == "jump");
        actions[0].Deadzone.Should().Be(0.7);
        actions[0].Events.Should().ContainSingle();
        actions[0].Events[0].EventType.Should().Be("key");
    }
}

