using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Nexus.SampleHost;
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
var httpClient = new HttpClient();
var hasModelClient = ModelConfiguration.TryCreateFromEnvironment(out var modelConfiguration);
var modelClient = hasModelClient && modelConfiguration is not null
    ? ModelClientFactory.Create(modelConfiguration, httpClient)
    : null;

async Task<string> ProcessRequestAsync(string input, CancellationToken cancellationToken)
{
    var bus = new EventBus();
    bus.Publish(new UserRequest(input));
    var rt = new Runtime(bus, 0);

    await correlationSensor.SenseAsync(rt, cancellationToken);
    await goalSensor.SenseAsync(rt, cancellationToken);
    await laneSensor.SenseAsync(rt, cancellationToken);

    var response = bus.GetOrDefault<AiResponse>();
    if (response is not null)
        return response.Text;

    if (modelClient is null)
        return "No model configured. Run scripts/install.sh to configure OpenAI, Anthropic, or Gemini.";

    return await modelClient.GenerateAsync(input, cancellationToken);
}

var discordToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
var discordChannelId = Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID");
if (!string.IsNullOrWhiteSpace(discordToken) && !string.IsNullOrWhiteSpace(discordChannelId))
{
    Console.WriteLine("Nexus SampleHost started in Discord mode.");
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var bridge = new DiscordChannelBridge(httpClient, discordToken, discordChannelId);
    await bridge.RunAsync(ProcessRequestAsync, cts.Token);
}
else
{
    Console.WriteLine("Nexus SampleHost started. Type a request (or 'quit' to exit):");
    if (modelConfiguration is not null)
        Console.WriteLine($"Model provider configured: {modelConfiguration.Provider} ({modelConfiguration.Model})");

    while (true)
    {
        Console.Write("> ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            break;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var responseText = await ProcessRequestAsync(input, cts.Token);

        var bus = new EventBus();
        bus.Publish(new UserRequest(input));
        var rt = new Runtime(bus, 0);
        await correlationSensor.SenseAsync(rt, cts.Token);
        await goalSensor.SenseAsync(rt, cts.Token);
        await laneSensor.SenseAsync(rt, cts.Token);
        var goal = bus.GetOrDefault<GoalSelected>();
        var lane = bus.GetOrDefault<LaneSelected>();

        Console.WriteLine($"  Goal: {goal?.Goal} ({goal?.Confidence:P0}), Lane: {lane?.Lane}");
        Console.WriteLine($"  Response: {responseText}");
    }
}

Console.WriteLine("Nexus SampleHost stopped.");
