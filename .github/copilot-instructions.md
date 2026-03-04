# Copilot Instructions for UtilityAi.Vitruvian

## Project Overview

**UtilityAi.Vitruvian** is a modular, multi-purpose bot host built on top of [mrrasmussendk/UtilityAi](https://github.com/mrrasmussendk/UtilityAi). It enables third-party DLLs (plugins) to register AI actions, considerations, and sensors while the host applies governance: goal/lane routing, cooldowns, conflict resolution, cost/risk penalties, and hysteresis to prevent oscillation.

**Language / Runtime:** C# 13, .NET 10, nullable enabled, implicit usings enabled.

---

## Repository Layout

```
UtilityAi.Vitruvian.sln
├── src/
│   ├── UtilityAi.Vitruvian.Abstractions/  ← Enums, facts, interfaces
│   ├── UtilityAi.Vitruvian.Runtime/       ← Sensors, modules, strategy, DI
│   ├── UtilityAi.Vitruvian.PluginSdk/     ← Attributes + metadata provider
│   ├── UtilityAi.Vitruvian.PluginHost/    ← Plugin loader (AssemblyLoadContext)
│   └── UtilityAi.Vitruvian.Hitl/          ← Human-in-the-loop gate (optional)
├── samples/
│   └── Vitruvian.SampleHost/              ← Console REPL / Discord demo host
└── tests/
    └── UtilityAi.Vitruvian.Tests/         ← xUnit tests
```

---

## How to Build

> Requires [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download).

```bash
dotnet build UtilityAi.Vitruvian.sln
```

---

## How to Test

Tests use **xUnit** and live in `tests/UtilityAi.Vitruvian.Tests/`.

```bash
dotnet test UtilityAi.Vitruvian.sln
```

To run a single test class:

```bash
dotnet test --filter "FullyQualifiedName~VitruvianGovernedSelectionStrategyTests"
```

---

## How to Run the Sample Host

```bash
dotnet run --project samples/Vitruvian.SampleHost
```

For guided setup (model provider + deployment mode):

```bash
./scripts/install.sh
source .env.Vitruvian
dotnet run --project samples/Vitruvian.SampleHost
```

---

## Coding Conventions

- **Nullable reference types** are enabled project-wide — always annotate types correctly and avoid `null!` suppressions unless truly necessary.
- **Implicit usings** are enabled — do not add redundant `using System;` / `using System.Collections.Generic;` etc.
- Follow existing file-scoped namespace style: `namespace UtilityAi.Vitruvian.<Project>;`
- Use **`sealed`** on leaf classes that are not designed for inheritance.
- New public types in `src/` require corresponding tests in `tests/UtilityAi.Vitruvian.Tests/`.
- Test classes use the pattern `<ClassUnderTest>Tests` and use `[Fact]` / `[Theory]` attributes from xUnit.
- Do **not** add `using Xunit;` inside test files — it is included via `<Using Include="Xunit" />` in the test project.

---

## Architecture Patterns

### Goal / Lane Routing

- `GoalRouterSensor` publishes `GoalSelected` facts and `LaneRouterSensor` publishes `LaneSelected` facts based on keyword matching on `UserRequest`.
- `VitruvianGovernedSelectionStrategy` filters proposals to those matching the current goal + lane (with progressive relaxation when no exact match exists).

### Plugin Metadata

- Use the attributes from `UtilityAi.Vitruvian.PluginSdk.Attributes` to annotate `ICapabilityModule` implementations:
  `[VitruvianCapability]`, `[VitruvianGoals]`, `[VitruvianLane]`, `[VitruvianCost]`, `[VitruvianRisk]`, `[VitruvianCooldown]`
- `AttributeMetadataProvider` reads these attributes at load time and exposes `ProposalMetadata` to the governance strategy.

### Governance

| Concern      | Mechanism                                                                 |
|--------------|---------------------------------------------------------------------------|
| Cooldowns    | `GovernanceMemoryProjectionSensor` + `CooldownState` facts                |
| Conflicts    | `ProposalMetadata.ConflictIds` / `ConflictTags` checked per tick          |
| Cost/Risk    | `effectiveScore = utility − (CostWeight × cost) − (RiskWeight × risk)`   |
| Hysteresis   | Previous winner re-selected when score is within `StickinessBonus` margin |

### Plugin Loading

`PluginLoader` (in `UtilityAi.Vitruvian.PluginHost`) uses `AssemblyLoadContext` to load DLLs from a `plugins/` folder and discovers all `ICapabilityModule` and `ISensor` types automatically.

---

## Writing a Plugin

1. Target **net10.0** in your class library.
2. Reference `UtilityAi.Vitruvian.PluginSdk`.
3. Implement `ICapabilityModule` and decorate with Vitruvian SDK attributes.
4. Build and drop the DLL into `<SampleHost-output>/plugins/`.

```csharp
[VitruvianCapability("my-domain", priority: 5)]
[VitruvianGoals(GoalTag.Answer, GoalTag.Summarize)]
[VitruvianLane(Lane.Communicate)]
[VitruvianCost(0.1)]
[VitruvianRisk(0.0)]
[VitruvianCooldown("my-domain.action", secondsTtl: 30)]
public sealed class MyModule : ICapabilityModule
{
    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        yield return new Proposal(
            id: "my-domain.answer",
            cons: [new ConstantValue(0.7)],
            act: _ => Task.CompletedTask
        );
    }
}
```
