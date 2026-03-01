using System.Reflection;
using UtilityAi.Compass.PluginHost;

namespace UtilityAi.Compass.Tests;

public class PluginLoaderTests
{
    [Fact]
    public void LoadFromFolder_DoesNotThrow_WhenFolderDoesNotExist()
    {
        var loader = new PluginLoader();
        loader.LoadFromFolder("/nonexistent/path");
        Assert.Empty(loader.DiscoverModules());
    }

    [Fact]
    public void LoadAssembly_AddsAssemblyForDiscovery()
    {
        var loader = new PluginLoader();
        loader.LoadAssembly(Assembly.GetExecutingAssembly());
        var manifests = loader.GetManifests().ToList();
        Assert.Single(manifests);
    }

    [Fact]
    public void LoadFromFolder_SkipsInvalidDllFiles()
    {
        var loader = new PluginLoader();
        var tempFolder = Path.Combine(Path.GetTempPath(), $"plugin-loader-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempFolder);

        try
        {
            File.WriteAllText(Path.Combine(tempFolder, "invalid.dll"), "not a dll");

            loader.LoadFromFolder(tempFolder);

            Assert.Empty(loader.GetManifests());
        }
        finally
        {
            Directory.Delete(tempFolder, recursive: true);
        }
    }
}
