using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UtilityAi.Capabilities;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Runtime;
using UtilityAi.Compass.Runtime.Strategy;
using UtilityAi.Memory;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;
using UtilityAi.Utils;

namespace Compass.SampleHost;

internal sealed class RequestProcessor(IHost host, CompassGovernedSelectionStrategy strategy, IModelClient? modelClient)
{
    private const int MaxConversationTurns = 50;
    private static readonly TimeSpan ConversationRetentionWindow = TimeSpan.FromHours(1);

    public async Task<(GoalSelected? Goal, LaneSelected? Lane, string Response)> ProcessAsync(string input, CancellationToken cancellationToken)
    {
        GoalSelected? goal = null;
        LaneSelected? lane = null;
        string responseText;

        var requests = await CompoundRequestOrchestrator.PlanRequestsAsync(modelClient, input, cancellationToken);
        if (requests.Count > 1)
        {
            var allResponses = new List<string>();
            foreach (var request in requests)
            {
                var (g, l, response) = await RunSingleRequestAsync(request, cancellationToken);
                allResponses.Add(response);
                goal ??= g;
                lane ??= l;
            }

            responseText = string.Join("\n\n", allResponses);
        }
        else
        {
            (goal, lane, responseText) = await RunSingleRequestAsync(input, cancellationToken);
        }

        await StoreConversationTurnAsync(input, responseText, cancellationToken);
        return (goal, lane, responseText);
    }

    private async Task<(GoalSelected? Goal, LaneSelected? Lane, string Response)> RunSingleRequestAsync(string input, CancellationToken cancellationToken)
    {
        var bus = new EventBus();
        bus.Publish(new UserRequest(input));

        var requestOrchestrator = new UtilityAiOrchestrator(selector: strategy, stopAtZero: true, bus: bus);

        foreach (var sensor in host.Services.GetServices<ISensor>())
            requestOrchestrator.AddSensor(sensor);

        foreach (var module in host.Services.GetServices<ICapabilityModule>())
            requestOrchestrator.AddModule(module);

        await requestOrchestrator.RunAsync(maxTicks: 10, cancellationToken);

        var goal = bus.GetOrDefault<GoalSelected>();
        var lane = bus.GetOrDefault<LaneSelected>();
        var response = bus.GetOrDefault<AiResponse>();

        if (response is not null)
            return (goal, lane, response.Text);

        if (modelClient is null)
        {
            return (goal, lane,
                "No model configured. Run 'compass --setup' or scripts/install.sh (Linux/macOS) / scripts/install.ps1 (Windows).");
        }

        var responseText = await modelClient.GenerateAsync(input, cancellationToken);
        return (goal, lane, responseText);
    }

    private async Task StoreConversationTurnAsync(string input, string responseText, CancellationToken cancellationToken)
    {
        var memoryStore = host.Services.GetService<IMemoryStore>();
        if (memoryStore is null || string.IsNullOrWhiteSpace(responseText))
            return;

        await memoryStore.StoreAsync(
            new ConversationTurn
            {
                UserMessage = input,
                AssistantResponse = responseText
            },
            DateTimeOffset.UtcNow,
            cancellationToken);

        var count = await memoryStore.CountAsync<ConversationTurn>(cancellationToken);
        if (count > MaxConversationTurns)
            await memoryStore.PruneAsync(ConversationRetentionWindow, cancellationToken);
    }
}
