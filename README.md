# UtilityAi.Compass — Multi-Purpose Bot Host

A modular "multi-purpose bot host" built on [mrrasmussendk/UtilityAi](https://github.com/mrrasmussendk/UtilityAi) (v1.6.5).  
Allows many third-party DLLs to add actions/considerations/sensors without destabilising the agent by adding goal/lane routing and governance (cooldowns, conflicts, cost/risk penalties, hysteresis).

---

## How to Build

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)

```bash
git clone <this-repo> --recurse-submodules
cd Compass
dotnet build UtilityAi.Compass.sln
```

> **Note**: The UtilityAi dependency is included as a git submodule at `vendor/UtilityAi` pinned to tag **v1.6.5**.  
> If you cloned without `--recurse-submodules`, run `git submodule update --init --recursive`.

---

## How to Run the Compass CLI

```bash
dotnet run --project src/UtilityAi.Compass.Cli
```

If no Compass model setup exists, the host will attempt to launch the platform installer script automatically on startup.

For guided setup (model provider + deployment mode), run:

```bash
dotnet run --project src/UtilityAi.Compass.Cli -- --setup
```

If installed as a .NET tool:

```bash
compass --setup
```

A simple REPL will start:

```
Compass CLI started. Type a request (or 'quit' to exit):
> summarize this document
  Goal: Summarize (85%), Lane: Communicate
> quit
```

To load plugins, copy plugin DLLs into a `plugins/` folder next to the executable before running.

You can also install modules with the host command:

- CLI command: `/install-module /absolute/path/MyPlugin.dll`
- CLI command: `/install-module Package.Id@1.2.3`
- Startup args: `dotnet run --project src/UtilityAi.Compass.Cli -- --install-module Package.Id@1.2.3`

Installed modules are copied into the host `plugins/` folder and loaded after restarting the host.

If `DISCORD_BOT_TOKEN` and `DISCORD_CHANNEL_ID` are set, the CLI switches to Discord mode and polls the configured channel for user messages.

### Built-in model provider integration

`UtilityAi.Compass.Cli` includes a shared model client abstraction with provider adapters for:

- OpenAI (`COMPASS_MODEL_PROVIDER=openai`, `OPENAI_API_KEY`)
- Anthropic (`COMPASS_MODEL_PROVIDER=anthropic`, `ANTHROPIC_API_KEY`)
- Gemini (`COMPASS_MODEL_PROVIDER=gemini`, `GEMINI_API_KEY`)

Optional:

- `COMPASS_MODEL_NAME` overrides the default model name per provider.
- `COMPASS_MODEL_MAX_TOKENS` sets Anthropic `max_tokens` (default `512`).
- `DISCORD_POLL_INTERVAL_SECONDS` and `DISCORD_MESSAGE_LIMIT` tune Discord polling behavior.

The sample `Compass.SamplePlugin.OpenAi` now also includes `SkillMarkdownModule`, which loads its system instructions from `skill.md`. Because it uses the shared `IModelClient` abstraction, the same module works with OpenAI, Anthropic, and Gemini.

---

## How to Write a Plugin

1. Create a .NET class library targeting **net10.0**.
2. Add a project reference to `UtilityAi.Compass.PluginSdk` (or reference its DLL).
3. Implement `ICapabilityModule` and/or `ISensor` from the UtilityAi framework.
4. Decorate your module with Compass SDK attributes:

```csharp
using UtilityAi.Capabilities;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.PluginSdk.Attributes;

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
            act: _ => { /* do work */ return Task.CompletedTask; }
        );
    }
}
```

5. Build and drop the DLL into the `plugins/` folder (see below).

---

## How to Drop DLLs into /plugins

1. Build your plugin: `dotnet publish -c Release`
2. Copy the output DLL (and dependencies) to `<SampleHost-output>/plugins/`.
3. Run the sample host — it will discover all `ICapabilityModule` and `ISensor` types automatically.

The `PluginLoader` in `UtilityAi.Compass.PluginHost` uses `AssemblyLoadContext` to load assemblies from a folder path.

---

## How Governance Works

### Goal / Lane Routing

Each tick the `GoalRouterSensor` publishes a `GoalSelected` fact and `LaneRouterSensor` publishes a `LaneSelected` fact based on heuristic keyword matching on the `UserRequest`.

`CompassGovernedSelectionStrategy` then filters the scored proposals to those matching the current goal + lane (with progressive relaxation).

### Cooldowns

If a proposal's `ProposalMetadata` has a `CooldownKeyTemplate` and `CooldownTtl`, the `GovernanceMemoryProjectionSensor` reads `ProposalExecutionRecord` entries from `InMemoryStore` and publishes `CooldownState` facts.  
The selection strategy either **hard-drops** or **penalises** a proposal on cooldown, depending on `GovernanceConfig.HardDropOnCooldown`.

### Conflicts

If a `ProposalMetadata` declares `ConflictIds` or `ConflictTags`, and a higher-utility proposal with one of those IDs or tags has already been selected in the same tick, the conflicting proposal is dropped.

### Cost / Risk Penalties

Effective score = `utility − (CostWeight × EstimatedCost) − (RiskWeight × RiskLevel)`.  
Weights are configurable via `GovernanceConfig`.

### Hysteresis

After selecting a winner, the strategy checks whether the previous winner (`LastWinner` fact from `InMemoryStore`) is still in the candidate list.  
If `lastWinner.EffectiveScore + StickinessBonus ≥ best.EffectiveScore − HysteresisEpsilon`, the previous winner is re-selected — preventing oscillation between similarly-scored proposals.

---

## Solution Layout

```
UtilityAi.Compass.sln
├── vendor/UtilityAi/                    ← git submodule @ v1.6.5
├── src/
│   ├── UtilityAi.Compass.Abstractions/    ← Enums, facts, interfaces
│   ├── UtilityAi.Compass.Runtime/         ← Sensors, modules, strategy, DI
│   ├── UtilityAi.Compass.PluginSdk/       ← Attributes + metadata provider
│   ├── UtilityAi.Compass.PluginHost/      ← Plugin loader (folder + AppDomain)
│   └── UtilityAi.Compass.Hitl/            ← Human-in-the-loop gate (optional)
├── samples/
│   ├── Compass.SampleHost/                ← Console REPL demo host
│   └── Compass.SamplePlugin.Basic/        ← Example third-party plugin
└── tests/
    └── UtilityAi.Compass.Tests/           ← xUnit tests (17 tests)
```

## CI package build

GitHub Actions workflow `.github/workflows/build-pack.yml` builds/tests on Linux and Windows and packs a cross-platform .NET tool NuGet package for `UtilityAi.Compass.Cli`.
