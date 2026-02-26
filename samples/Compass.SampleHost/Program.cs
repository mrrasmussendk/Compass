using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Compass.SampleHost;
using UtilityAi.Memory;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.PluginHost;
using UtilityAi.Compass.PluginSdk.MetadataProvider;
using UtilityAi.Compass.Runtime.DI;
using UtilityAi.Compass.Runtime.Sensors;
using UtilityAi.Orchestration;
using UtilityAi.Utils;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddUtilityAiCompass(opts =>
{
    opts.EnableGovernanceFinalizer = true;
    opts.EnableHitl = false;
});

builder.Services.AddSingleton<AttributeMetadataProvider>();
builder.Services.AddSingleton<IProposalMetadataProvider>(sp => sp.GetRequiredService<AttributeMetadataProvider>());

// Register the host-level model client so plugins receive it via DI.
// The concrete provider (OpenAI, Anthropic, Gemini) is chosen by env config.
var httpClient = new HttpClient();
var hasModelClient = ModelConfiguration.TryCreateFromEnvironment(out var modelConfiguration);
IModelClient? modelClient = hasModelClient && modelConfiguration is not null
    ? ModelClientFactory.Create(modelConfiguration, httpClient)
    : null;
if (modelClient is not null)
    builder.Services.AddSingleton<IModelClient>(modelClient);

var pluginsPath = Path.Combine(AppContext.BaseDirectory, "plugins");
builder.Services.AddCompassPluginsFromFolder(pluginsPath);

var host = builder.Build();

var store = host.Services.GetRequiredService<IMemoryStore>();
var metadataProvider = host.Services.GetRequiredService<IProposalMetadataProvider>();
var strategy = host.Services.GetRequiredService<UtilityAi.Compass.Runtime.Strategy.CompassGovernedSelectionStrategy>();

var correlationSensor = host.Services.GetRequiredService<CorrelationSensor>();
var goalSensor = host.Services.GetRequiredService<GoalRouterSensor>();
var laneSensor = host.Services.GetRequiredService<LaneRouterSensor>();

async Task<(GoalSelected? Goal, LaneSelected? Lane, string Response)> ProcessRequestAsync(string input, CancellationToken cancellationToken)
{
    var bus = new EventBus();
    bus.Publish(new UserRequest(input));
    var rt = new Runtime(bus, 0);

    await correlationSensor.SenseAsync(rt, cancellationToken);
    await goalSensor.SenseAsync(rt, cancellationToken);
    await laneSensor.SenseAsync(rt, cancellationToken);

    var goal = bus.GetOrDefault<GoalSelected>();
    var lane = bus.GetOrDefault<LaneSelected>();
    var response = bus.GetOrDefault<AiResponse>();
    if (response is not null)
        return (goal, lane, response.Text);

    if (modelClient is null)
        return (goal, lane, "No model configured. Run scripts/install.sh to configure OpenAI, Anthropic, or Gemini.");

    var modelResponse = await modelClient.GenerateAsync(input, cancellationToken);
    return (goal, lane, modelResponse);
}

var discordToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
var discordChannelId = Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID");
if (!string.IsNullOrWhiteSpace(discordToken) && !string.IsNullOrWhiteSpace(discordChannelId))
{
    Console.WriteLine("Compass SampleHost started in Discord mode.");
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var bridge = new DiscordChannelBridge(httpClient, discordToken, discordChannelId);
    await bridge.RunAsync(async (message, token) =>
    {
        var (_, _, response) = await ProcessRequestAsync(message, token);
        return response;
    }, cts.Token);
}
else
{
    Console.WriteLine("Compass SampleHost started. Type a request (or 'quit' to exit):");
    if (modelConfiguration is not null)
        Console.WriteLine($"Model provider configured: {modelConfiguration.Provider} ({modelConfiguration.Model})");

    while (true)
    {
        Console.Write("> ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            break;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var (goal, lane, responseText) = await ProcessRequestAsync(input, cts.Token);

        Console.WriteLine($"  Goal: {goal?.Goal} ({goal?.Confidence:P0}), Lane: {lane?.Lane}");
        Console.WriteLine($"  Response: {responseText}");
    }
}

Console.WriteLine("Compass SampleHost stopped.");
