using System.Text.Json;
using FluentAssertions;
using GodotMCP.Application.Tools;
using GodotMCP.Tests.Fixtures;

namespace GodotMCP.Tests.Integration;

public class FilesToolsTests
{
    [Fact]
    public async Task GetFileInfo_ShouldReturnContentAndMetadata()
    {
        var (root, resolver, files) = FixtureFactory.CreateProject();
        try
        {
            var fileName = "notes.txt";
            var absolute = Path.Combine(root, fileName);
            var content = "Hello from test";
            await files.WriteAsync(absolute, content);

            var result = await GodotTools.GetFileInfoAsync(files, resolver, root, fileName);
            result.Success.Should().BeTrue();

            var json = JsonSerializer.Serialize(result.Data);
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("Content").GetString().Should().Be(content);
            doc.RootElement.GetProperty("Size").GetInt64().Should().Be(content.Length);
            doc.RootElement.GetProperty("RelativePath").GetString().Should().Be(fileName);
        }
        finally
        {
            FixtureFactory.Cleanup(root);
        }
    }
}
