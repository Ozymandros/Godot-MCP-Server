using GodotMCP.Application.Tools;
using GodotMCP.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using StreamJsonRpc;

try
{
    // Keep stdout reserved for JSON-RPC messages only.
    Console.SetOut(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });

    var services = new ServiceCollection();
    services.AddGodotInfrastructure(Directory.GetCurrentDirectory());
    services.AddSingleton<GodotTools>();
    var provider = services.BuildServiceProvider();

    var tools = provider.GetRequiredService<GodotTools>();
    using var jsonRpc = JsonRpc.Attach(Console.OpenStandardOutput(), Console.OpenStandardInput(), tools);
    await jsonRpc.Completion.ConfigureAwait(false);
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"Fatal server error: {ex.Message}").ConfigureAwait(false);
    Environment.ExitCode = 1;
}
