using Microsoft.Extensions.DependencyInjection;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.StandardModules;
using UtilityAi.Compass.StandardModules.DI;

namespace UtilityAi.Compass.Tests;

public class StandardModulesDiTests
{
    private sealed class StubModelClient : IModelClient
    {
        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult("stub");

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ModelResponse { Text = "stub" });
    }

    [Fact]
    public void AddCompassStandardModules_RegistersAllFourModules()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IModelClient>(new StubModelClient());
        services.AddCompassStandardModules();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<FileReadModule>());
        Assert.NotNull(provider.GetRequiredService<FileCreationModule>());
        Assert.NotNull(provider.GetRequiredService<SummarizationModule>());
        Assert.NotNull(provider.GetRequiredService<WebSearchModule>());
    }

    [Fact]
    public void AddCompassStandardModules_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddCompassStandardModules();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddCompassStandardModules_DoesNotThrow_WhenNoModelClientRegistered()
    {
        var services = new ServiceCollection();
        services.AddCompassStandardModules();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<FileReadModule>());
        Assert.NotNull(provider.GetRequiredService<FileCreationModule>());
        Assert.NotNull(provider.GetRequiredService<SummarizationModule>());
        Assert.NotNull(provider.GetRequiredService<WebSearchModule>());
    }
}
