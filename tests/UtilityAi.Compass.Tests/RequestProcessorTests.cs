using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Cli;
using UtilityAi.Compass.PluginSdk.MetadataProvider;
using UtilityAi.Compass.Runtime.Strategy;
using UtilityAi.Memory;
using UtilityAi.Sensor;

namespace UtilityAi.Compass.Tests;

public class RequestProcessorTests
{
    private sealed class ExecuteIntentSensor : ISensor
    {
        public Task SenseAsync(UtilityAi.Utils.Runtime rt, CancellationToken ct)
        {
            rt.Bus.Publish(new GoalSelected(GoalTag.Execute, 0.95, "test"));
            rt.Bus.Publish(new LaneSelected(Lane.Execute));
            return Task.CompletedTask;
        }
    }

    private sealed class CountingModelClient : IModelClient
    {
        public int StringCalls { get; private set; }
        public int StructuredCalls { get; private set; }

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        {
            StringCalls++;
            return Task.FromResult("fallback");
        }

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            StructuredCalls++;
            return Task.FromResult(new ModelResponse { Text = "not-json" });
        }
    }

    [Fact]
    public async Task ProcessAsync_ReturnsTransactionalCapabilityMessage_WhenExecuteHasNoCapability()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ISensor, ExecuteIntentSensor>();
        var host = builder.Build();
        var model = new CountingModelClient();
        var strategy = new CompassGovernedSelectionStrategy(new InMemoryStore(), new AttributeMetadataProvider());
        var processor = new RequestProcessor(host, strategy, model);

        var (_, _, response) = await processor.ProcessAsync("delete file /tmp/a.txt", CancellationToken.None);

        Assert.Contains("can't complete this action transactionally", response, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, model.StringCalls);
        Assert.Equal(1, model.StructuredCalls);
    }
}
