using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Nexus.Abstractions;
using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Nexus.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace Nexus.SamplePlugin.Basic;

[NexusCapability("commandline", priority: 3)]
[NexusGoals(GoalTag.Execute)]
[NexusLane(Lane.Execute)]
[NexusSideEffects(SideEffectLevel.ReadOnly)]
[NexusCost(0.1)]
public sealed class CommandLineReadModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var score = CommandLineCommonActions.Score(
            rt.Bus.GetOrDefault<UserRequest>()?.Text,
            CommandLineCommonActions.ReadKeywords);
        if (score <= 0) yield break;

        yield return new Proposal(
            id: "commandline.read",
            cons: [new ConstantValue(score)],
            act: _ =>
            {
                rt.Bus.Publish(new AiResponse("Command line read action selected."));
                return Task.CompletedTask;
            }
        ) { Description = "Read data from command line context" };
    }
}

[NexusCapability("commandline", priority: 3)]
[NexusGoals(GoalTag.Execute)]
[NexusLane(Lane.Execute)]
[NexusSideEffects(SideEffectLevel.Write)]
[NexusCost(0.3)]
public sealed class CommandLineWriteModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var score = CommandLineCommonActions.Score(
            rt.Bus.GetOrDefault<UserRequest>()?.Text,
            CommandLineCommonActions.WriteKeywords);
        if (score <= 0) yield break;

        yield return new Proposal(
            id: "commandline.write",
            cons: [new ConstantValue(score)],
            act: _ =>
            {
                rt.Bus.Publish(new AiResponse("Command line write action selected."));
                return Task.CompletedTask;
            }
        ) { Description = "Write data in command line context" };
    }
}

[NexusCapability("commandline", priority: 3)]
[NexusGoals(GoalTag.Execute)]
[NexusLane(Lane.Execute)]
[NexusSideEffects(SideEffectLevel.Write)]
[NexusCost(0.25)]
public sealed class CommandLineUpdateModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var score = CommandLineCommonActions.Score(
            rt.Bus.GetOrDefault<UserRequest>()?.Text,
            CommandLineCommonActions.UpdateKeywords);
        if (score <= 0) yield break;

        yield return new Proposal(
            id: "commandline.update",
            cons: [new ConstantValue(score)],
            act: _ =>
            {
                rt.Bus.Publish(new AiResponse("Command line update action selected."));
                return Task.CompletedTask;
            }
        ) { Description = "Update data in command line context" };
    }
}
