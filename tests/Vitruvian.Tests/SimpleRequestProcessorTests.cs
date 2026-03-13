using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VitruvianAbstractions;
using VitruvianAbstractions.Interfaces;
using VitruvianPluginSdk.Attributes;
using VitruvianRuntime;
using VitruvianRuntime.Routing;
using Xunit;

namespace VitruvianTests;

public sealed class SimpleRequestProcessorTests
{
    private sealed class StubModelClient : IModelClient
    {
        private readonly string _response;

        public StubModelClient(string response)
        {
            _response = response;
        }

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ModelResponse { Text = _response });

        public Task<string> CompleteAsync(string systemMessage, string userMessage, IReadOnlyList<ModelTool>? tools = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);
    }

    [RequiresPermission(ModuleAccess.Read)]
    private sealed class TestModule : IVitruvianModule
    {
        public string Domain { get; }
        public string Description { get; }
        private readonly string _response;

        public TestModule(string domain, string description, string response = "test response")
        {
            Domain = domain;
            Description = description;
            _response = response;
        }

        public Task<string> ExecuteAsync(string request, string? userId, CancellationToken ct)
            => Task.FromResult(_response);
    }

    [Fact]
    public async Task ProcessAsync_WithNoModules_ReturnsFallback()
    {
        var modelClient = new StubModelClient("fallback response");
        var router = new ModuleRouter(modelClient);
        var processor = new RequestProcessor(router, modelClient);

        var result = await processor.ProcessAsync("test request", CancellationToken.None);

        Assert.Equal("fallback response", result);
    }

    [Fact]
    public async Task ProcessAsync_WithMatchingModule_ExecutesModule()
    {
        var modelClient = new StubModelClient("{\"domain\":\"test-module\",\"confidence\":0.9,\"reason\":\"matches\"}");
        var router = new ModuleRouter(modelClient);
        var module = new TestModule("test-module", "Test module", "module executed");
        var processor = new RequestProcessor(router, modelClient);
        processor.RegisterModule(module);

        var result = await processor.ProcessAsync("test request", CancellationToken.None);

        Assert.Equal("module executed", result);
    }

    [Fact]
    public async Task ProcessAsync_WithNoModelClient_ReturnsError()
    {
        var router = new ModuleRouter();
        var processor = new RequestProcessor(router, null);

        var result = await processor.ProcessAsync("test request", CancellationToken.None);

        Assert.Contains("No model configured", result);
    }

    [Fact]
    public void RegisterModule_ThenUnregister_ReturnsTrue()
    {
        var router = new ModuleRouter();
        var processor = new RequestProcessor(router, null);
        var module = new TestModule("installed-mod", "An installed module");

        processor.RegisterModule(module);

        Assert.True(processor.IsModuleRegistered("installed-mod"));
        Assert.True(processor.UnregisterModule("installed-mod"));
        Assert.False(processor.IsModuleRegistered("installed-mod"));
    }

    [Fact]
    public void RegisterModule_CalledTwice_CanStillUnregister()
    {
        var router = new ModuleRouter();
        var processor = new RequestProcessor(router, null);
        var module1 = new TestModule("dup-mod", "First registration");
        var module2 = new TestModule("dup-mod", "Second registration");

        processor.RegisterModule(module1);
        processor.RegisterModule(module2);

        Assert.True(processor.IsModuleRegistered("dup-mod"));
        Assert.True(processor.UnregisterModule("dup-mod"));
        Assert.False(processor.IsModuleRegistered("dup-mod"));
    }

    [Fact]
    public void IsModuleRegistered_WithNullOrWhitespace_ReturnsFalse()
    {
        var router = new ModuleRouter();
        var processor = new RequestProcessor(router, null);

        Assert.False(processor.IsModuleRegistered(null!));
        Assert.False(processor.IsModuleRegistered(""));
        Assert.False(processor.IsModuleRegistered("  "));
    }

    [Fact]
    public async Task ProcessAsync_WithLogger_LogsMessages()
    {
        var logs = new List<string>();
        var modelClient = new StubModelClient("fallback response");
        var router = new ModuleRouter(modelClient);
        var processor = new RequestProcessor(router, modelClient, logger: msg => logs.Add(msg));

        await processor.ProcessAsync("test", CancellationToken.None);

        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.Contains("[GOAP]"));
    }

    [Fact]
    public async Task ProcessAsync_WithNullLogger_DoesNotThrow()
    {
        var modelClient = new StubModelClient("ok");
        var router = new ModuleRouter(modelClient);
        var processor = new RequestProcessor(router, modelClient, logger: null);

        var result = await processor.ProcessAsync("test", CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task ProcessAsync_WithModuleContextFactory_WrapsModules()
    {
        var wrapperCalled = false;
        var modelClient = new StubModelClient(
            """[{"step_id":"s1","module":"test-module","description":"test","input":"test","depends_on":[]}]""");
        var router = new ModuleRouter(modelClient);
        var module = new TestModule("test-module", "Test module", "wrapped response");
        var processor = new RequestProcessor(
            router,
            modelClient,
            moduleContextFactory: (mod, client) =>
            {
                wrapperCalled = true;
                return mod; // Return original module (just tracking the call)
            });
        processor.RegisterModule(module);

        var result = await processor.ProcessAsync("test request", CancellationToken.None);

        Assert.True(wrapperCalled);
    }

    [Fact]
    public async Task ProcessAsync_WithoutModuleContextFactory_UsesOriginalModules()
    {
        var modelClient = new StubModelClient(
            """[{"step_id":"s1","module":"test-module","description":"test","input":"test","depends_on":[]}]""");
        var router = new ModuleRouter(modelClient);
        var module = new TestModule("test-module", "Test module", "original response");
        var processor = new RequestProcessor(router, modelClient, moduleContextFactory: null);
        processor.RegisterModule(module);

        var result = await processor.ProcessAsync("test request", CancellationToken.None);

        Assert.Equal("original response", result);
    }

    [Fact]
    public void Planner_IsAccessible()
    {
        var modelClient = new StubModelClient("ok");
        var router = new ModuleRouter(modelClient);
        var processor = new RequestProcessor(router, modelClient);

        Assert.NotNull(processor.Planner);
    }
}
