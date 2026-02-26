# UtilityAi.Compass

A modular orchestration framework built on top of [UtilityAi](https://github.com/mrrasmussendk/UtilityAi) that adds governance, plugin hosting, goal routing, and human-in-the-loop support.

## Projects

| Project | Description |
|---|---|
| `UtilityAi.Compass.Abstractions` | Shared enums, facts, and interfaces |
| `UtilityAi.Compass.Runtime` | Core sensors, modules, selection strategy, DI extensions |
| `UtilityAi.Compass.PluginSdk` | Attributes and metadata provider for plugin authors |
| `UtilityAi.Compass.PluginHost` | Assembly-based plugin loader and DI integration |
| `UtilityAi.Compass.Hitl` | Human-in-the-loop gate module and facts |
| `Compass.SampleHost` | Console host demonstrating the framework |
| `Compass.SamplePlugin.Basic` | Example plugin using the SDK |

## Quick Start

```csharp
builder.Services.AddUtilityAiCompass(opts =>
{
    opts.EnableGovernanceFinalizer = true;
});
builder.Services.AddSingleton<AttributeMetadataProvider>();
builder.Services.AddSingleton<IProposalMetadataProvider>(sp =>
    sp.GetRequiredService<AttributeMetadataProvider>());
```

## Key Concepts

- **GoalTag**: Classifies user intent (Answer, Clarify, Summarize, Execute, Approve, Stop)
- **Lane**: Routes proposals to processing pipelines (Interpret, Plan, Execute, Communicate, Safety, Housekeeping)
- **CompassGovernedSelectionStrategy**: Selects proposals based on goal/lane filtering, conflict resolution, cooldowns, and cost/risk penalties
- **PluginLoader**: Discovers `ICapabilityModule`, `ISensor`, and `IOrchestrationSink` implementations from assemblies
- **HitlGateModule**: Intercepts destructive requests (delete, deploy, override) and routes them through a human approval channel
