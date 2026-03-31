using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using GodotMCP.Core.Interfaces;
using Xunit;

namespace GodotMCP.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="GodotMCP.Infrastructure.Services.GodotFileService"/> covering basic
/// read/write/delete and enumeration workflows.
/// </summary>
public class GodotFileServiceTests
{
    /// <summary>
    /// Verifies that writing, reading, checking existence, enumerating, and deleting
    /// resources via <see cref="GodotMCP.Infrastructure.Services.GodotFileService"/> works as expected.
    /// </summary>
    [Fact]
    public async Task ReadWriteExistsDeleteEnumerate_Workflow()
    {
        var root = Path.Combine(Path.GetTempPath(), "godotfilesvc", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            IPathResolver resolver = new GodotMCP.Infrastructure.Services.PathResolver(root);
            var svc = new GodotMCP.Infrastructure.Services.GodotFileService(resolver);

            var path = "res://assets/test.txt";
            await svc.WriteAsync(path, "hello");
            Assert.True(svc.Exists(path));
            var content = await svc.ReadAsync(path);
            Assert.Equal("hello", content);

            var dir = "res://assets";
            var files = svc.EnumerateFiles(dir, "*.txt", false).ToList();
            Assert.True(files.Count >= 1);

            await svc.DeleteAsync(path);
            Assert.False(svc.Exists(path));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
