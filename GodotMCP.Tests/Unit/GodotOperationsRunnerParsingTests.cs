using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using GodotMCP.Core.Interfaces;
using GodotMCP.Core.Models;
using GodotMCP.Infrastructure.Process;
using GodotMCP.Infrastructure.Services;
using GodotMCP.Tests.TestIsolation;

namespace GodotMCP.Tests.Unit;

/// <summary>
/// Tests that validate parsing of the JSON response envelope emitted by the
/// operations runner script.
/// </summary>
public class GodotOperationsRunnerParsingTests
{
    [Fact]
    public async Task Runner_Parses_AttachScript_Response()
    {
        var root = AssemblyStartup.CreateSandboxDirectory("godotopsparse");
        var resolver = new PathResolver(root);
        string? capturedArgs = null;

        IGodotCliService fakeCli = new FakeCliService((args) =>
        {
            capturedArgs = args;
            var stdout = "{\"schemaVersion\":\"1.0\",\"requestId\":\"rid-attach\",\"success\":true,\"message\":\"attached\",\"data\":{\"nodeName\":\"Player\"}}";
            return Task.FromResult(new ToolResult(true, "ok", new Dictionary<string,string> { ["stdout"] = stdout }));
        });

        var runner = new GodotOperationsRunner(fakeCli, resolver);
        var payload = new Dictionary<string, object>
        {
            ["schemaVersion"] = "1.0",
            ["requestId"] = "rid-attach",
            ["payload"] = new Dictionary<string, object>
            {
                ["scenePath"] = "res://scenes/Main.tscn",
                ["nodeName"] = "Player",
                ["scriptPath"] = "res://scripts/Player.gd"
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var res = await runner.RunOperationAsync("attach_script", json);
        Assert.True(res.Success);
        Assert.True(res.Data?.ContainsKey("nodeName"));
        Assert.Equal("Player", res.Data!["nodeName"]);
    }

    private sealed class FakeCliService : IGodotCliService
    {
        private readonly Func<string, Task<ToolResult>> callback;
        public FakeCliService(Func<string, Task<ToolResult>> cb) => callback = cb;
        public Task<ToolResult> RunAsync(string arguments, CancellationToken cancellationToken = default) => callback(arguments);
    }
}
