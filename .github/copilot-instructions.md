# Copilot Instructions for UtilityAi.Compass

## Project Overview

**UtilityAi.Compass** is a modular, multi-purpose bot host built on top of [mrrasmussendk/UtilityAi](https://github.com/mrrasmussendk/UtilityAi). It enables third-party DLLs (plugins) to register AI actions, considerations, and sensors while the host applies governance: goal/lane routing, cooldowns, conflict resolution, cost/risk penalties, and hysteresis to prevent oscillation.

**Language / Runtime:** C# 13, .NET 10, nullable enabled, implicit usings enabled.

---

## Repository Layout

```
UtilityAi.Compass.sln
├── vendor/UtilityAi/                    ← git submodule @ v1.6.5
├── src/
│   ├── UtilityAi.Compass.Abstractions/  ← Enums, facts, interfaces
│   ├── UtilityAi.Compass.Runtime/       ← Sensors, modules, strategy, DI
│   ├── UtilityAi.Compass.PluginSdk/     ← Attributes + metadata provider
│   ├── UtilityAi.Compass.PluginHost/    ← Plugin loader (AssemblyLoadContext)
│   └── UtilityAi.Compass.Hitl/          ← Human-in-the-loop gate (optional)
├── samples/
│   └── Compass.SampleHost/              ← Console REPL / Discord demo host
└── tests/
    └── UtilityAi.Compass.Tests/         ← xUnit tests
```

---

## How to Build

> Requires [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download).  
> The `vendor/UtilityAi` submodule must be initialised: `git submodule update --init --recursive`

```bash
dotnet build UtilityAi.Compass.sln
```

---

## How to Test

Tests use **xUnit** and live in `tests/UtilityAi.Compass.Tests/`.

```bash
dotnet test UtilityAi.Compass.sln
```

To run a single test class:

```bash
dotnet test --filter "FullyQualifiedName~CompassGovernedSelectionStrategyTests"
```

---

## How to Run the Sample Host

```bash
dotnet run --project samples/Compass.SampleHost
```

For guided setup (model provider + deployment mode):

```bash
./scripts/install.sh
source .env.compass
dotnet run --project samples/Compass.SampleHost
```

---

## Coding Conventions

- **Nullable reference types** are enabled project-wide — always annotate types correctly and avoid `null!` suppressions unless truly necessary.
- **Implicit usings** are enabled — do not add redundant `using System;` / `using System.Collections.Generic;` etc.
- Follow existing file-scoped namespace style: `namespace UtilityAi.Compass.<Project>;`
- Use **`sealed`** on leaf classes that are not designed for inheritance.
- New public types in `src/` require corresponding tests in `tests/UtilityAi.Compass.Tests/`.
- Test classes use the pattern `<ClassUnderTest>Tests` and use `[Fact]` / `[Theory]` attributes from xUnit.
- Do **not** add `using Xunit;` inside test files — it is included via `<Using Include="Xunit" />` in the test project.

---

## Architecture Patterns

### Goal / Lane Routing

- `GoalRouterSensor` publishes `GoalSelected` facts and `LaneRouterSensor` publishes `LaneSelected` facts based on keyword matching on `UserRequest`.
- `CompassGovernedSelectionStrategy` filters proposals to those matching the current goal + lane (with progressive relaxation when no exact match exists).

### Plugin Metadata

- Use the attributes from `UtilityAi.Compass.PluginSdk.Attributes` to annotate `ICapabilityModule` implementations:
  `[CompassCapability]`, `[CompassGoals]`, `[CompassLane]`, `[CompassCost]`, `[CompassRisk]`, `[CompassCooldown]`
- `AttributeMetadataProvider` reads these attributes at load time and exposes `ProposalMetadata` to the governance strategy.

### Governance

| Concern      | Mechanism                                                                 |
|--------------|---------------------------------------------------------------------------|
| Cooldowns    | `GovernanceMemoryProjectionSensor` + `CooldownState` facts                |
| Conflicts    | `ProposalMetadata.ConflictIds` / `ConflictTags` checked per tick          |
| Cost/Risk    | `effectiveScore = utility − (CostWeight × cost) − (RiskWeight × risk)`   |
| Hysteresis   | Previous winner re-selected when score is within `StickinessBonus` margin |

### Plugin Loading

`PluginLoader` (in `UtilityAi.Compass.PluginHost`) uses `AssemblyLoadContext` to load DLLs from a `plugins/` folder and discovers all `ICapabilityModule` and `ISensor` types automatically.

---

## Writing a Plugin

1. Target **net10.0** in your class library.
2. Reference `UtilityAi.Compass.PluginSdk`.
3. Implement `ICapabilityModule` and decorate with Compass SDK attributes.
4. Build and drop the DLL into `<SampleHost-output>/plugins/`.

```csharp
[CompassCapability("my-domain", priority: 5)]
[CompassGoals(GoalTag.Answer, GoalTag.Summarize)]
[CompassLane(Lane.Communicate)]
[CompassCost(0.1)]
[CompassRisk(0.0)]
[CompassCooldown("my-domain.action", secondsTtl: 30)]
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
