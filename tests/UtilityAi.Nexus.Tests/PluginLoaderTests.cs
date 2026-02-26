using System.Reflection;
using UtilityAi.Nexus.PluginHost;

namespace UtilityAi.Nexus.Tests;

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
}
