# UtilityAi.Compass — Multi-Purpose Bot Host

A modular "multi-purpose bot host" built on [mrrasmussendk/UtilityAi](https://github.com/mrrasmussendk/UtilityAi) (v1.6.5).  
Allows many third-party DLLs to add actions/considerations/sensors without destabilising the agent by adding goal/lane routing and governance (cooldowns, conflicts, cost/risk penalties, hysteresis).

---

## Request Flow

Every user interaction follows a deterministic pipeline:

```
User Input
  │
  ▼
┌──────────────────────────────────────┐
│  1. Sensors publish facts            │
│     • CorrelationSensor → unique ID  │
│     • GoalRouterSensor  → GoalTag    │
│     • LaneRouterSensor  → Lane       │
│     • CliIntentSensor   → CliIntent  │
│     • WorkflowStateSensor            │
│     • ValidationStateSensor          │
│     • GovernanceMemoryProjection     │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  2. Modules propose actions          │
│     (built-in + plugin modules)      │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  3. Governance selects a winner      │
│     (see "Governance Pipeline")      │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  4. GovernanceFinalizerModule        │
│     records execution history        │
└──────────────────────────────────────┘
```

### Sensors

| Sensor | Publishes | Purpose |
|---|---|---|
| `CorrelationSensor` | `CorrelationId` | Unique ID per tick for tracing |
| `GoalRouterSensor` | `GoalSelected` | Detects intent — Answer, Clarify, Summarize, Execute, Approve, Stop — with model confidence and workflow context (`active_workflow`, `recent_step`, `set_variables`) |
| `LaneRouterSensor` | `LaneSelected` | Maps goal to processing lane — Interpret, Plan, Execute, Communicate, Safety, Housekeeping — with low-confidence execute/approve fallback to `Interpret` |
| `CliIntentSensor` | `CliIntent` | Detects Read / Write / Update verb and target route |
| `WorkflowStateSensor` | `ActiveWorkflow`, `StepReady` | Projects active workflow run state from memory store |
| `ValidationStateSensor` | `NeedsValidation`, `ValidationOutcome` | Requests and surfaces step/workflow validation results |
| `GovernanceMemoryProjectionSensor` | `LastWinner`, `CooldownState` | Hysteresis tracking and cooldown projection |

### Modules

| Module | Role |
|---|---|
| `CliActionModule` | Proposes registered `ICliAction` instances when `CliIntent` matches |
| `RoutingBootstrapModule` | Proposes a clarification prompt when `GoalSelected.Confidence < 0.4` |
| `GovernanceFinalizerModule` | Records `ProposalExecutionRecord` and `LastWinner` to `IMemoryStore` after each tick |
| `HitlGateModule` | Intercepts destructive requests and routes them through a human approval channel (see [Security](#security)) |

---

## Security

### Side-Effect Levels

Every proposal can declare a `SideEffectLevel`:

| Level | Meaning |
|---|---|
| `ReadOnly` | No state changes — safe to execute without approval |
| `Write` | Creates or modifies data |
| `Destructive` | Deletes data, deploys, or overrides — triggers HITL gate |

The governance strategy applies heavier cost/risk penalties to proposals with higher side-effect levels.

### Human-in-the-Loop (HITL) Safety Gate

`HitlGateModule` prevents destructive operations from executing without human approval.

**Trigger keywords:** `delete`, `override`, `deploy`

**Flow:**

1. `HitlGateModule` detects a destructive `UserRequest`.
2. A `hitl.create-request` proposal is raised (utility 0.85).
3. On selection the module publishes `HitlPending` and `HitlRequest`, then sends the request to `IHumanDecisionChannel`.
4. A `hitl.wait-for-decision` proposal polls `IHumanDecisionChannel.TryReceiveDecisionAsync()`.
5. Once the human responds:
   - **Approved** → `HitlApproved` fact published; the original action may proceed.
   - **Rejected** → `HitlRejected` fact published with a reason; the request is dropped.

Plugins can integrate with HITL by checking for `HitlApproved` / `HitlRejected` facts on the EventBus before performing irreversible work.

### Workflow Validation Pipeline

Multi-step workflows support validation gates at both step and workflow level:

1. After a step completes, the module may publish a `NeedsValidation` fact.
2. `ValidationStateSensor` projects the pending validation and its result (`ValidationOutcome`).
3. Possible outcomes: **Pass**, **FailRetryable**, **FailFatal**.
4. On failure the system consults a `RepairType` directive:

| RepairType | Behaviour |
|---|---|
| `RetryStep` | Re-execute the failed step |
| `Replan` | Regenerate remaining workflow steps |
| `SwitchWorkflow` | Transition to an alternative workflow |
| `AskUser` | Request clarification from the user |
| `Hitl` | Escalate to a human operator via the HITL gate |
| `Abort` | Fail the workflow immediately |

### Plugin Sandboxing

Plugins are loaded via `PluginLoader` using `Assembly.LoadFrom()`. The host discovers only types implementing known contracts (`ICapabilityModule`, `ISensor`, `IOrchestrationSink`, `ICliAction`). Plugins run in the same process as the host; isolation is contract-based rather than process-based. All plugin proposals pass through the same governance pipeline (goal/lane filtering, cooldowns, conflict resolution, cost/risk penalties) as built-in modules.

---

## How to Build

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)

```bash
git clone <this-repo>
cd Compass
dotnet build UtilityAi.Compass.sln
```

> **Note**: Compass now consumes **UtilityAi v1.6.5** from NuGet (`UtilityAi` package), so no submodule initialization is required.

---

## How to Run the Compass CLI

```bash
dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli
```

If no Compass model setup exists, the host will attempt to launch the platform installer script automatically on startup.

For guided setup (model provider + deployment mode), run:

```bash
dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli -- --setup
```

To show available CLI arguments:

```bash
dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli -- --help
```

To list currently installed plugin modules:

```bash
dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli -- --list-modules
```

To install the CLI as a .NET tool:

```bash
dotnet tool install --global UtilityAi.Compass.Cli
```

If installed as a .NET tool:

```bash
compass --setup
compass --help
compass --list-modules
compass --new-module MyPlugin
```

A simple REPL will start:

```
Compass CLI started. Type a request (or 'quit' to exit):
> summarize this document
  Goal: Summarize (85%), Lane: Communicate
> quit
```

To load plugins, copy plugin DLLs into a `plugins/` folder next to the executable before running.

You can also manage modules with host commands:

- CLI command: `/help`
- CLI command: `/list-modules`
- CLI command: `/install-module /absolute/path/MyPlugin.dll`
- CLI command: `/install-module Package.Id@1.2.3`
- CLI command: `/new-module MyPlugin [/absolute/output/path]`
- Example: `/install-module UtilityAi.Compass.WeatherModule@1.0.1`
- Startup args: `dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli -- --install-module Package.Id@1.2.3`
- Startup args: `dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli -- --new-module MyPlugin`

Installed modules are copied into the host `plugins/` folder and loaded after restarting the host.

### Create a new module with the CLI

Use the scaffold command to generate a starter plugin project:

```bash
compass --new-module MyPlugin
# or
dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli -- --new-module MyPlugin [/absolute/output/path]
```

This creates `<output-path>/MyPlugin/` with:

- `MyPlugin.csproj` (net10.0 class library with `UtilityAi` reference)
- `MyPluginModule.cs` (example `ICapabilityModule` proposal)

Then build and install it:

```bash
dotnet build /absolute/output/path/MyPlugin
compass --install-module /absolute/output/path/MyPlugin/bin/Debug/net10.0/MyPlugin.dll
```

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
- `COMPASS_MEMORY_CONNECTION_STRING` sets the SQLite memory database (defaults to local `appdb/compass-memory.db` under the app base directory when unset).

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

`CompassGovernedSelectionStrategy` processes every tick through a five-step pipeline:

### 1. Workflow Commitment

When an `ActiveWorkflow` is running and has not expired its `StickinessUntilUtc`, only proposals belonging to that workflow — or system-level proposals (`askuser.*`, `validate.*`, `repair.*`) — are allowed through. All other proposals are filtered out.

### 2. Goal / Lane Routing

Each tick the `GoalRouterSensor` publishes a `GoalSelected` fact using model-based classification from `UserRequest` plus workflow context (`ActiveWorkflow` and latest `StepResult`), and `LaneRouterSensor` publishes `LaneSelected` with confidence-aware safety fallback for side-effectful intents.

The strategy filters proposals to those matching the current goal + lane. Matching is progressively relaxed: goal + lane → goal only → lane only → untagged fallback.

### 3. Conflict Resolution

Proposals are evaluated in descending utility order. If a `ProposalMetadata` declares `ConflictIds` or `ConflictTags`, and a higher-utility proposal with one of those IDs or tags has already been chosen in the same tick, the conflicting proposal is dropped.

### 4. Cooldowns

If a proposal's `ProposalMetadata` has a `CooldownKeyTemplate` and `CooldownTtl`, the `GovernanceMemoryProjectionSensor` reads `ProposalExecutionRecord` entries from `InMemoryStore` and publishes `CooldownState` facts.  
The selection strategy either **hard-drops** or **penalises** a proposal on cooldown, depending on `GovernanceConfig.HardDropOnCooldown`.

### 5. Cost / Risk Penalties & Hysteresis

Effective score = `utility − (CostWeight × EstimatedCost) − (RiskWeight × RiskLevel)`.

After selecting the highest-scoring candidate, the strategy checks whether the previous winner (`LastWinner` fact from `InMemoryStore`) is still in the candidate list. If `lastWinner.EffectiveScore + StickinessBonus ≥ best.EffectiveScore − HysteresisEpsilon`, the previous winner is re-selected — preventing oscillation between similarly-scored proposals.

### Configuration (`GovernanceConfig`)

| Parameter | Default | Description |
|---|---|---|
| `CostWeight` | 0.2 | Multiplier for `EstimatedCost` penalty |
| `RiskWeight` | 0.2 | Multiplier for `RiskLevel` penalty |
| `HysteresisEpsilon` | 0.05 | Minimum score delta required to switch winners |
| `StickinessBonus` | 0.02 | Bonus added to the previous winner's score |
| `HardDropOnCooldown` | false | When true, proposals on cooldown are removed; when false, penalised |
| `CooldownPenalty` | 0.8 | Score reduction applied when soft-dropping a cooled-down proposal |

---

## Solution Layout

```
UtilityAi.Compass.sln
├── src/
│   ├── UtilityAi.Compass.Abstractions/    ← Enums, facts, interfaces
│   ├── UtilityAi.Compass.Runtime/         ← Sensors, modules, strategy, DI
│   ├── UtilityAi.Compass.PluginSdk/       ← Attributes + metadata provider
│   ├── UtilityAi.Compass.PluginHost/      ← Plugin loader (folder + AssemblyLoadContext)
│   └── UtilityAi.Compass.Hitl/            ← Human-in-the-loop gate (optional)
├── samples/
│   ├── Compass.SampleHost/                ← Console REPL demo host
│   └── Compass.SamplePlugin.Basic/        ← Example third-party plugin
└── tests/
    └── UtilityAi.Compass.Tests/           ← xUnit tests
```

## CI package build

GitHub Actions workflow `.github/workflows/build-pack.yml` builds/tests on Linux and Windows and packs a cross-platform .NET tool NuGet package for `UtilityAi.Compass.Cli`.

GitHub Actions workflow `.github/workflows/master-version-bump.yml` runs only on pushes to `master` and bumps package versions using this repository's custom rollover policy:

- `major.minor.patch` increments `patch` until `9`
- `x.y.9` rolls to `x.(y+1).0`
- `x.9.9` rolls to `(x+1).0.0`
