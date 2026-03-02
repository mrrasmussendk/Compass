using System.Text;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Memory;
using UtilityAi.Utils;

namespace UtilityAi.Compass.StandardModules;

/// <summary>
/// Standard module that handles general conversational requests using the
/// host-provided <see cref="IModelClient"/>. Acts as a fallback for requests
/// that don't match more specialized modules. Maintains conversation history
/// for context-aware responses.
/// </summary>
[CompassCapability("conversation", priority: 1)]
[CompassGoals(GoalTag.Answer)]
[CompassLane(Lane.Communicate)]
[CompassCost(0.4)]
[CompassRisk(0.0)]
[CompassSideEffects(SideEffectLevel.ReadOnly)]
public sealed class ConversationModule : ICapabilityModule
{
    private readonly IModelClient? _modelClient;
    private readonly IMemoryStore? _memoryStore;

    public ConversationModule(IModelClient? modelClient = null, IMemoryStore? memoryStore = null)
    {
        _modelClient = modelClient;
        _memoryStore = memoryStore;
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        if (_modelClient is null) yield break;
        var goal = rt.Bus.GetOrDefault<GoalSelected>();
        var lane = rt.Bus.GetOrDefault<LaneSelected>();
        if (goal?.Goal == GoalTag.Execute || lane?.Lane == Lane.Execute) yield break;
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        yield return new Proposal(
            id: "conversation.chat",
            cons: [new ConstantValue(0.9)],  // Higher than WebSearch (0.8) to win for conversational requests
            act: async ct =>
            {
                // Retrieve recent conversation history if memory store is available
                var prompt = request.Text;
                if (_memoryStore is not null)
                {
                    var history = await _memoryStore.RecallAsync<ConversationTurn>(
                        new MemoryQuery { MaxResults = 2, SortOrder = SortOrder.NewestFirst },
                        ct);

                    if (history.Count > 0)
                    {
                        var mostRecent = history.First();
                        
                        // Token-efficient format: Use minimal markers and truncate if needed
                        var prevUser = mostRecent.Fact.UserMessage;
                        var prevAssistant = mostRecent.Fact.AssistantResponse;
                        
                        // If assistant's previous response is very long (>500 chars), 
                        // keep the beginning (greeting/question) and end (options/question)
                        if (prevAssistant.Length > 500)
                        {
                            var start = prevAssistant.Substring(0, 200);
                            var end = prevAssistant.Substring(prevAssistant.Length - 200);
                            prevAssistant = $"{start}...[middle content omitted]...{end}";
                        }
                        
                        prompt = $"[Previous]\nU: {prevUser}\nA: {prevAssistant}\n\n[Current]\nU: {request.Text}";
                    }
                }

                var response = await _modelClient.GenerateAsync(
                    new ModelRequest
                    {
                        Prompt = prompt,
                        SystemMessage = "You are a helpful conversational AI assistant. When you see conversation history, use it to maintain context. If the user responds with a number, word, or brief phrase, check the previous context to understand what they're referring to - they're likely choosing an option or continuing the conversation.",
                        MaxTokens = 512
                    },
                    ct);

                // Note: Conversation storage is handled by ProcessRequestAsync in the host
                // to avoid duplicate storage across different modules

                rt.Bus.Publish(new AiResponse(response.Text));
            }
        ) { Description = "Respond to general conversational requests with conversation history" };
    }
}
