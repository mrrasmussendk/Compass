using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UtilityAi.Memory;
using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Nexus.Abstractions.Interfaces;
using UtilityAi.Nexus.PluginHost;
using UtilityAi.Nexus.PluginSdk.MetadataProvider;
using UtilityAi.Nexus.Runtime.DI;
using UtilityAi.Nexus.Runtime.Sensors;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddUtilityAiNexus(opts =>
{
    opts.EnableGovernanceFinalizer = true;
    opts.EnableHitl = false;
});

builder.Services.AddSingleton<AttributeMetadataProvider>();
builder.Services.AddSingleton<IProposalMetadataProvider>(sp => sp.GetRequiredService<AttributeMetadataProvider>());

var pluginsPath = Path.Combine(AppContext.BaseDirectory, "plugins");
builder.Services.AddNexusPluginsFromFolder(pluginsPath);

var host = builder.Build();

var store = host.Services.GetRequiredService<IMemoryStore>();
var metadataProvider = host.Services.GetRequiredService<IProposalMetadataProvider>();
var strategy = host.Services.GetRequiredService<UtilityAi.Nexus.Runtime.Strategy.NexusGovernedSelectionStrategy>();

var correlationSensor = host.Services.GetRequiredService<CorrelationSensor>();
var goalSensor = host.Services.GetRequiredService<GoalRouterSensor>();
var laneSensor = host.Services.GetRequiredService<LaneRouterSensor>();

Console.WriteLine("Nexus SampleHost started. Type a request (or 'quit' to exit):");

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        break;

    var bus = new EventBus();
    bus.Publish(new UserRequest(input));
    var rt = new Runtime(bus, 0);

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await correlationSensor.SenseAsync(rt, cts.Token);
    await goalSensor.SenseAsync(rt, cts.Token);
    await laneSensor.SenseAsync(rt, cts.Token);

    var goal = bus.GetOrDefault<GoalSelected>();
    var lane = bus.GetOrDefault<LaneSelected>();
    Console.WriteLine($"  Goal: {goal?.Goal} ({goal?.Confidence:P0}), Lane: {lane?.Lane}");

    var response = bus.GetOrDefault<AiResponse>();
    if (response is not null)
        Console.WriteLine($"  Response: {response.Text}");
}

Console.WriteLine("Nexus SampleHost stopped.");
