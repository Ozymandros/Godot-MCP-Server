using FluentAssertions;
using GodotMCP.Core.Interfaces;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Services;
using GodotMCP.Core.Models;

namespace GodotMCP.Tests.Unit;

public class GodotOperationsRunnerTests
{
    [Fact]
    public async Task RunOperationAsync_ShouldInvokeCliWithScriptAndPayload()
    {
        var root = Path.Combine(Path.GetTempPath(), "godotops", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var resolver = new PathResolver(root);
            string? capturedArgs = null;

            IGodotCliService fakeCli = new FakeCliService((args) =>
            {
                capturedArgs = args;
                return Task.FromResult(new ToolResult(true, "ok", new Dictionary<string,string> { ["stdout"] = "{}" }));
            });

            var runner = new GodotOperationsRunner(fakeCli, resolver);
            var payload = "{\"hello\":\"world\"}";
            var res = await runner.RunOperationAsync("noop", payload);

            res.Success.Should().BeTrue();
            capturedArgs.Should().NotBeNull();
            capturedArgs!.Should().Contain("--script");
            capturedArgs.Should().Contain("noop");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private sealed class FakeCliService : IGodotCliService
    {
        private readonly Func<string, Task<ToolResult>> callback;

        public FakeCliService(Func<string, Task<ToolResult>> cb) => callback = cb;

        public Task<ToolResult> RunAsync(string arguments, CancellationToken cancellationToken = default)
            => callback(arguments);
    }
}
