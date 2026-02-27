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
        File.Copy(typeof(UtilityAi.Compass.StandardModules.FileReadModule).Assembly.Location, pluginDll, overwrite: true);

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
            await using (var source = File.OpenRead(typeof(UtilityAi.Compass.StandardModules.FileReadModule).Assembly.Location))
                await source.CopyToAsync(stream);
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

    [Fact]
    public async Task InstallAsync_RejectsNonUtilityAiDll()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var hostDll = typeof(ModuleInstaller).Assembly.Location;
        var pluginDll = Path.Combine(root, "not-a-module.dll");
        var pluginsDir = Path.Combine(root, "plugins");
        File.Copy(hostDll, pluginDll, overwrite: true);

        try
        {
            var message = await ModuleInstaller.InstallAsync(pluginDll, pluginsDir);

            Assert.Contains("not a compatible UtilityAI module assembly", message);
            Assert.False(File.Exists(Path.Combine(pluginsDir, "not-a-module.dll")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task InstallAsync_RejectsPackageWithoutCompatibleFrameworkDlls()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var nupkgPath = Path.Combine(root, "plugin.nupkg");
        var pluginsDir = Path.Combine(root, "plugins");

        using (var archive = ZipFile.Open(nupkgPath, ZipArchiveMode.Create))
        {
            var libEntry = archive.CreateEntry("lib/net11.0/example.plugin.dll");
            await using var stream = libEntry.Open();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync("fake");
        }

        try
        {
            var message = await ModuleInstaller.InstallAsync(nupkgPath, pluginsDir);

            Assert.Contains("does not contain compatible .dll files", message);
            Assert.False(Directory.Exists(pluginsDir) && Directory.EnumerateFiles(pluginsDir, "*.dll").Any());
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

    [Fact]
    public void ListInstalledModules_ReturnsEmpty_WhenFolderDoesNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var modules = ModuleInstaller.ListInstalledModules(root);
        Assert.Empty(modules);
    }

    [Fact]
    public async Task ListInstalledModules_ReturnsSortedDllNamesOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "z-plugin.dll"), "a");
        await File.WriteAllTextAsync(Path.Combine(root, "a-plugin.dll"), "b");
        await File.WriteAllTextAsync(Path.Combine(root, "readme.txt"), "c");

        try
        {
            var modules = ModuleInstaller.ListInstalledModules(root);
            Assert.Equal(["a-plugin.dll", "z-plugin.dll"], modules);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
