using Compass.SampleHost;
using System.IO.Compression;

namespace UtilityAi.Compass.Tests;

public class ModuleInstallerTests
{
    [Fact]
    public void TryParsePackageReference_ParsesExpectedFormat()
    {
        var ok = ModuleInstaller.TryParsePackageReference("My.Plugin@1.2.3", out var packageId, out var packageVersion);

        Assert.True(ok);
        Assert.Equal("My.Plugin", packageId);
        Assert.Equal("1.2.3", packageVersion);
    }

    [Fact]
    public async Task InstallAsync_CopiesDllIntoPluginsFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var pluginDll = Path.Combine(root, "sample-plugin.dll");
        var pluginsDir = Path.Combine(root, "plugins");
        await File.WriteAllTextAsync(pluginDll, "fake");

        try
        {
            var message = await ModuleInstaller.InstallAsync(pluginDll, pluginsDir);

            Assert.Contains("Installed module DLL", message);
            Assert.True(File.Exists(Path.Combine(pluginsDir, "sample-plugin.dll")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InstallAsync_ExtractsDllsFromNupkg()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var nupkgPath = Path.Combine(root, "plugin.nupkg");
        var pluginsDir = Path.Combine(root, "plugins");

        using (var archive = ZipFile.Open(nupkgPath, ZipArchiveMode.Create))
        {
            var libEntry = archive.CreateEntry("lib/net10.0/example.plugin.dll");
            await using (var stream = libEntry.Open())
            await using (var writer = new StreamWriter(stream))
                await writer.WriteAsync("fake");
        }

        try
        {
            var message = await ModuleInstaller.InstallAsync(nupkgPath, pluginsDir);

            Assert.Contains("Installed 1 module assembly file", message);
            Assert.True(File.Exists(Path.Combine(pluginsDir, "example.plugin.dll")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("/install-module My.Plugin@1.2.3", "My.Plugin@1.2.3")]
    [InlineData("   /install-module   /tmp/plugin.dll   ", "/tmp/plugin.dll")]
    public void TryParseInstallCommand_ParsesExpectedFormat(string input, string expected)
    {
        var ok = ModuleInstaller.TryParseInstallCommand(input, out var moduleSpec);

        Assert.True(ok);
        Assert.Equal(expected, moduleSpec);
    }
}
