# UtilityAi.Compass

**UtilityAi.Compass** is a governed host for UtilityAI-based assistants.

If you are evaluating Compass for practical use, there are two main workflows:

1. **Use the CLI tool** to run Compass, scaffold modules, and install plugin packages.
2. **Extend Compass** by writing your own capability modules, sensors, and optional CLI actions.

This README is organized around those two workflows first.

**Try it now (30 seconds):**

```bash
git clone https://github.com/mrrasmussendk/Compass.git && cd Compass && dotnet run --project samples/Compass.SampleHost
```

**Database/local DB:** no external database required. Guided setup defaults to local SQLite file memory (`Data Source=appdb/compass-memory.db`) unless you choose a third-party connection string.

---

## Table of Contents

- [Who this is for](#who-this-is-for)
- [Quick start (5 minutes)](#quick-start-5-minutes)
- [CLI-first usage](#cli-first-usage)
  - [Run Compass CLI](#run-compass-cli)
  - [Command reference](#command-reference)
  - [Interactive mode commands](#interactive-mode-commands)
  - [Module installation and scaffold flows](#module-installation-and-scaffold-flows)
- [Extension-first usage (plugins)](#extension-first-usage-plugins)
  - [Plugin architecture at a glance](#plugin-architecture-at-a-glance)
  - [Step-by-step: build your first capability module](#step-by-step-build-your-first-capability-module)
  - [Governance metadata attributes](#governance-metadata-attributes)
  - [Optional: add sensors and CLI actions](#optional-add-sensors-and-cli-actions)
  - [Build and load plugin into Compass](#build-and-load-plugin-into-compass)
- [How governance works when your module runs](#how-governance-works-when-your-module-runs)
- [Safety and human-in-the-loop](#safety-and-human-in-the-loop)
- [Repository layout](#repository-layout)
- [Documentation map](#documentation-map)

---

## Who this is for

| Your goal | Start here |
|---|---|
| **Use Compass now** | [Quick start (5 minutes)](#quick-start-5-minutes) → [CLI-first usage](#cli-first-usage) |
| **Build extensions/plugins** | [Extension-first usage (plugins)](#extension-first-usage-plugins) → [docs/EXTENDING.md](docs/EXTENDING.md) |
| **Understand internals/contribute** | [How governance works when your module runs](#how-governance-works-when-your-module-runs) → [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) |

---

## Quick start (5 minutes)

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)
- Git

### Clone, build, test

```bash
git clone https://github.com/mrrasmussendk/Compass.git
cd Compass
dotnet build UtilityAi.Compass.sln
dotnet test UtilityAi.Compass.sln
```

### Optional guided setup (recommended)

```bash
./scripts/install.sh
# Windows PowerShell: .\scripts\install.ps1
```

Guided setup is profile-aware (`dev`, `personal`, `team`, `prod`) and writes:

- `.env.compass.<profile>` for profile-specific settings
- `.env.compass` with `COMPASS_PROFILE=<profile>` to mark the active profile

On repeated setup runs, you can press Enter on the API key prompt to reuse the cached key already stored for that profile.

### Run the sample host (same core runtime used by the CLI tool)

```bash
dotnet run --project samples/Compass.SampleHost
```

Expected startup (wording may vary by build/version):

```text
... started. Type a request (or 'quit' to exit):
```

---

## CLI-first usage

Compass ships as a CLI application (`compass`) and can also be executed directly via `dotnet run`.

### Run Compass CLI

#### Option A — run from source (repo clone)

```bash
dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli
```

#### Option B — install as a global .NET tool

```bash
dotnet tool install --global UtilityAi.Compass.Cli
compass --help
```

### Command reference

The CLI supports these startup arguments:

```text
--help
--setup
--list-modules
--install-module <path|package@version>
--new-module <Name> [OutputPath]
```

#### `--help`
Print available CLI arguments.

#### `--setup`
Runs the bundled installer script (`scripts/install.sh` or `scripts/install.ps1`) for interactive onboarding.

Setup flow:

1. Choose onboarding action (create/update profile or switch active profile).
2. Choose profile (`dev`, `personal`, `team`, `prod`).
3. Choose model provider (OpenAI / Anthropic / Gemini).
4. Enter API key (or leave blank to reuse cached key for that profile).
5. Enter model name (or accept default).
6. Choose deployment mode (local console or Discord).
7. Choose storage mode (default local SQLite or custom connection string).

#### `--list-modules`
Shows standard modules + plugin DLLs already installed in the runtime `plugins/` folder.
Built-in standard modules include file read/write, summarization, web search, and Gmail (read + draft).

#### `--install-module <path|package@version>`
Installs a module from either:

- local `.dll`
- local `.nupkg`
- NuGet package reference (`Package.Id@1.2.3`)

If the module manifest declares `requiredSecrets`, Compass validates them during install.
Missing values are prompted in interactive mode; installation fails if required values are not provided.

Examples:

```bash
compass --install-module /absolute/path/MyPlugin.dll
compass --install-module /absolute/path/MyPlugin.1.0.0.nupkg
compass --install-module Package.Id@1.2.3
```

#### `--new-module <Name> [OutputPath]`
Scaffolds a minimal module project directory.

Examples:

```bash
compass --new-module MyPlugin
compass --new-module MyPlugin /absolute/path/to/output
```

### Interactive mode commands

When you start CLI with no startup argument, it enters interactive mode.

In interactive mode, use:

- `/help`
- `/setup`
- `/list-modules`
- `/install-module <path|package@version>`
- `/new-module <Name> [OutputPath]`

You can also type plain natural-language requests (for example: "summarize this file") and Compass routes/executes the best proposal.

### Module installation and scaffold flows

#### Flow A: install an existing plugin

1. Build or obtain plugin artifact.
2. Run `compass --install-module ...`.
3. Restart Compass CLI (new modules are loaded on startup).
4. Run `compass --list-modules` to verify installation.

#### Flow B: scaffold and iterate quickly

1. Run `compass --new-module MyPlugin`.
2. Open generated files and implement your logic.
3. Build your module.
4. Install module with `--install-module`.
5. Restart CLI and test behavior.

---

## Extension-first usage (plugins)

Compass is designed to be extended with third-party modules while keeping execution governed and predictable.

### Plugin architecture at a glance

Plugins can provide one or more of:

- `ICapabilityModule` (primary action/proposal producer)
- `ISensor` (publishes facts used for routing/governance)
- `ICliAction` (discoverable CLI action endpoint)

At startup, `PluginLoader` discovers plugin types and registers them into DI.

### Step-by-step: build your first capability module

#### 1) Create a class library

```bash
dotnet new classlib -f net10.0 -n MyCompassPlugin
cd MyCompassPlugin
```

#### 2) Add dependencies

For metadata-driven governance, add the Compass SDK package and UtilityAI runtime dependency used for proposals.

```bash
dotnet add package UtilityAi.Compass.PluginSdk
dotnet add package UtilityAi
```

#### 3) Implement `ICapabilityModule`

```csharp
using UtilityAi.Capabilities;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Consideration.General;

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

#### 4) Build plugin

```bash
dotnet build
```

### Governance metadata attributes

| Attribute | What it controls |
|---|---|
| `CompassCapability` | Capability domain identity and priority |
| `CompassGoals` | Which `GoalTag` values this module should handle |
| `CompassLane` | Lane affinity for routing |
| `CompassCost` | Estimated cost penalty in scoring |
| `CompassRisk` | Risk penalty in scoring |
| `CompassCooldown` | Cooldown memory key + TTL |
| `CompassConflicts` | Optional conflict IDs/tags against other proposals |

### Optional: add sensors and CLI actions

#### Add a sensor (`ISensor`)
Use sensors to publish additional facts into the runtime bus. Facts can influence filtering, safety, cooldown behavior, or custom module logic.

#### Add a CLI action (`ICliAction`)
Use CLI actions when your plugin should expose explicit command-oriented behavior.

```csharp
using UtilityAi.Compass.Abstractions.CliAction;

public sealed class ReadConfigAction : ICliAction
{
    public CliVerb Verb => CliVerb.Read;
    public string Route => "config";
    public string Description => "Read configuration values";

    public Task<string> ExecuteAsync(string input, CancellationToken ct = default)
        => Task.FromResult("Current config: ...");
}
```

### Build and load plugin into Compass

You can load your plugin in two supported ways:

#### Option A — copy build output to `plugins/`

Place plugin DLLs next to the running executable under a `plugins/` folder.

#### Option B — install with CLI

```bash
compass --install-module /absolute/path/MyCompassPlugin.dll
```

Then restart Compass CLI.

---

## How governance works when your module runs

`CompassGovernedSelectionStrategy` applies governance stages in order:

1. Workflow commitment check
2. Goal + lane filtering (progressive relaxation if strict match is missing)
3. Conflict checks (`ConflictIds`, `ConflictTags`)
4. Cooldown checks (drop or penalty)
5. Effective score calculation + hysteresis

Scoring formula:

```text
effectiveScore = utility - (CostWeight * EstimatedCost) - (RiskWeight * RiskLevel)
```

Hysteresis (stickiness) helps avoid rapid winner flipping when scores are close.

---

## Safety and human-in-the-loop

Compass supports side-effect awareness:

- `ReadOnly`
- `Write`
- `Destructive`

`HitlGateModule` can intercept destructive requests (for example: delete/override/deploy intent) and route them through a human approval channel before execution continues.

---

## Repository layout

```text
UtilityAi.Compass.sln
├── src/
│   ├── UtilityAi.Compass.Abstractions/     # Enums, facts, interfaces
│   ├── UtilityAi.Compass.Runtime/          # Sensors, modules, strategy, DI
│   ├── UtilityAi.Compass.PluginSdk/        # Attributes + metadata provider
│   ├── UtilityAi.Compass.PluginHost/       # Plugin loader and DI integration
│   ├── UtilityAi.Compass.Hitl/             # Human-in-the-loop module/facts
│   ├── UtilityAi.Compass.StandardModules/  # Built-in reusable modules
│   ├── UtilityAi.Compass.WeatherModule/    # Example weather-oriented module
│   └── UtilityAi.Compass.Cli/              # CLI host/tooling
├── samples/
│   ├── Compass.SampleHost/                 # Console/Discord sample host
│   ├── Compass.SamplePlugin.Basic/         # Minimal plugin sample
│   └── Compass.SamplePlugin.OpenAi/        # Model-backed plugin sample
├── docs/
│   ├── INSTALL.md
│   ├── USING.md
│   ├── EXTENDING.md
│   └── CONTRIBUTING.md
└── tests/
    └── UtilityAi.Compass.Tests/
```

---

## Documentation map

- [docs/INSTALL.md](docs/INSTALL.md) — full setup and troubleshooting
- [docs/USING.md](docs/USING.md) — host runtime usage patterns
- [docs/EXTENDING.md](docs/EXTENDING.md) — plugin author guide
- [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) — contribution workflow
- [docs/README.md](docs/README.md) — high-level docs index

If your primary goal is adoption, start with CLI workflows.
If your primary goal is product differentiation, start with extension workflows.
