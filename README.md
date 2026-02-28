# UtilityAi.Compass

**UtilityAi.Compass** is a modular bot host built on top of [UtilityAi](https://github.com/mrrasmussendk/UtilityAi).
It helps you combine many built-in and third-party capabilities while keeping execution predictable through governance (routing, cooldowns, conflict checks, cost/risk penalties, and hysteresis).

## Start Here

### New to Compass?
- Follow the installation guide: [docs/INSTALL.md](docs/INSTALL.md)
- Build and run quickly (see [Quick Start](#quick-start))
- Try the sample host in REPL mode

### Extending Compass?
- Read [Extension Model](#extension-model)
- Scaffold a plugin with `--new-module`
- Use SDK attributes to participate in governance

---

## Quick Start

Requirements:
- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)
- Git

```bash
git clone https://github.com/mrrasmussendk/Compass.git
cd Compass
dotnet build UtilityAi.Compass.sln
dotnet test UtilityAi.Compass.sln
dotnet run --project samples/Compass.SampleHost
```

You should see a prompt similar to:

```text
Compass SampleHost started. Type a request (or 'quit' to exit):
> summarize this document
  Goal: Summarize (85%), Lane: Communicate
```

For guided provider setup (OpenAI / Anthropic / Gemini), run:

```bash
./scripts/install.sh
# Windows PowerShell: .\scripts\install.ps1
```

---

## What Compass Adds on Top of UtilityAi

Compass layers governance and hosting concerns on top of UtilityAi proposal selection:

- **Goal routing**: classify user intent (`GoalTag`)
- **Lane routing**: route work by processing lane (`Lane`)
- **Governed selection**: conflicts, cooldowns, cost/risk penalties, hysteresis
- **Plugin hosting**: load modules/sensors/CLI actions from external DLLs
- **Safety controls**: human-in-the-loop gate for destructive actions

### Request Pipeline

```text
UserRequest
  -> Sensors publish facts (GoalSelected, LaneSelected, CliIntent, CooldownState...)
  -> Modules propose actions (built-in + plugin)
  -> CompassGovernedSelectionStrategy picks winner
  -> GovernanceFinalizerModule stores execution history
```

---

## Extension Model

Compass is designed to be extended safely.

### Plugin author checklist

1. Create a `net10.0` class library.
2. Reference `UtilityAi.Compass.PluginSdk`.
3. Implement `ICapabilityModule` (and optionally `ISensor` / `ICliAction`).
4. Decorate module classes with Compass attributes.
5. Copy plugin output into the host `plugins/` folder (or install via CLI).

### Example capability module

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
            act: _ => Task.CompletedTask
        );
    }
}
```

### Metadata attributes and why they matter

| Attribute | Purpose in governance |
|---|---|
| `CompassCapability` | Stable capability identity + priority |
| `CompassGoals` | Declares which goals a proposal should match |
| `CompassLane` | Declares lane affinity for routing |
| `CompassCost` | Adds estimated cost penalty |
| `CompassRisk` | Adds risk penalty |
| `CompassCooldown` | Prevents rapid repeated execution |

---

## Governance in One View

`CompassGovernedSelectionStrategy` applies these stages each tick:

1. **Workflow commitment** (stick to active workflow when required)
2. **Goal/lane filtering** (with progressive relaxation)
3. **Conflict resolution** (`ConflictIds`, `ConflictTags`)
4. **Cooldown handling** (hard drop or penalty)
5. **Effective score and hysteresis**

Formula:

```text
effectiveScore = utility - (CostWeight * EstimatedCost) - (RiskWeight * RiskLevel)
```

If the previous winner is still competitive within stickiness bounds, Compass keeps it to reduce oscillation.

---

## Safety Model

### Side-effect levels

| Level | Meaning |
|---|---|
| `ReadOnly` | No persistent changes |
| `Write` | Creates/modifies state |
| `Destructive` | Deletes/overrides/deploys |

### Human-in-the-loop (HITL)

`HitlGateModule` intercepts destructive intents (for example: `delete`, `override`, `deploy`) and routes them through `IHumanDecisionChannel` before allowing execution.

---

## CLI and Module Operations

Run the Compass CLI:

```bash
dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli
```

Install as a global .NET tool (optional):

```bash
dotnet tool install --global UtilityAi.Compass.Cli
```

Common commands:

```bash
# setup and discovery
compass --setup
compass --help
compass --list-modules

# create/install modules
compass --new-module MyPlugin
compass --install-module /absolute/path/MyPlugin.dll
compass --install-module Package.Id@1.2.3
```

Equivalent in dev mode:

```bash
dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli -- --new-module MyPlugin
dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli -- --install-module Package.Id@1.2.3
```

---

## Solution Layout

```text
UtilityAi.Compass.sln
├── src/
│   ├── UtilityAi.Compass.Abstractions/     # Enums, facts, interfaces
│   ├── UtilityAi.Compass.Runtime/          # Sensors, modules, strategy, DI
│   ├── UtilityAi.Compass.PluginSdk/        # Attributes + metadata provider
│   ├── UtilityAi.Compass.PluginHost/       # Plugin loader and DI integration
│   ├── UtilityAi.Compass.Hitl/             # Human-in-the-loop module/facts
│   ├── UtilityAi.Compass.StandardModules/  # Reusable built-in modules
│   ├── UtilityAi.Compass.WeatherModule/    # Example/weather-oriented module
│   └── UtilityAi.Compass.Cli/              # CLI host and module tooling
├── samples/
│   ├── Compass.SampleHost/                 # Console/Discord sample host
│   ├── Compass.SamplePlugin.Basic/         # Minimal plugin sample
│   └── Compass.SamplePlugin.OpenAi/        # Model-backed sample plugin
├── docs/
│   ├── README.md
│   └── INSTALL.md
└── tests/
    └── UtilityAi.Compass.Tests/
```

---

## More Documentation

- [docs/README.md](docs/README.md) — concepts and project overview
- [docs/INSTALL.md](docs/INSTALL.md) — installation and troubleshooting

If you are building a plugin ecosystem, start with the sample plugin projects and follow the metadata conventions above so your modules remain predictable inside Compass governance.
