# Installation Guide

This guide walks you through installing and running **UtilityAi.Compass**.

---

## Prerequisites

| Requirement | Details |
|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download) | Build and run the solution |
| [Git](https://git-scm.com/) | Clone the repository and initialise the submodule |

Verify both are installed:

```bash
dotnet --version   # should print 10.x
git --version
```

---

## 1. Clone the Repository

Clone with the `--recurse-submodules` flag so that the **UtilityAi** dependency (pinned to **v1.6.5** at `vendor/UtilityAi`) is fetched automatically:

```bash
git clone https://github.com/mrrasmussendk/Compass.git --recurse-submodules
cd Compass
```

If you already cloned without `--recurse-submodules`, initialise the submodule manually:

```bash
git submodule update --init --recursive
```

---

## 2. Build

```bash
dotnet build UtilityAi.Compass.sln
```

A successful build compiles all projects listed in [Solution Layout](#solution-layout).

---

## 3. Run Tests

```bash
dotnet test UtilityAi.Compass.sln
```

To run a single test class:

```bash
dotnet test --filter "FullyQualifiedName~CompassGovernedSelectionStrategyTests"
```

---

## 4. Configure — Guided Setup (Recommended)

The interactive install script creates a `.env.compass` file with the required environment variables.

**Linux / macOS:**

```bash
./scripts/install.sh
```

**Windows (PowerShell):**

```powershell
.\scripts\install.ps1
```

The host auto-loads `.env.compass` at startup, so no manual sourcing step is required.

The script will prompt you to:

1. **Select a model provider** — OpenAI, Anthropic, or Gemini.
2. **Enter your API key** for the chosen provider.
3. **Choose a model name** (or accept the default).
4. **Select a deployment mode** — local console or Discord channel.

If you select Discord, you will also be prompted for `DISCORD_BOT_TOKEN` and `DISCORD_CHANNEL_ID`.

---

## 5. Configure — Manual Setup

If you prefer to set environment variables yourself, export the following before running the sample host:

| Variable | Required | Description |
|---|---|---|
| `COMPASS_MODEL_PROVIDER` | Yes | `openai`, `anthropic`, or `gemini` |
| `OPENAI_API_KEY` | When provider is `openai` | OpenAI API key |
| `ANTHROPIC_API_KEY` | When provider is `anthropic` | Anthropic API key |
| `GEMINI_API_KEY` | When provider is `gemini` | Gemini API key |
| `COMPASS_MODEL_NAME` | No | Overrides the default model name per provider |
| `COMPASS_MODEL_MAX_TOKENS` | No | Sets Anthropic `max_tokens` (default `512`) |
| `DISCORD_BOT_TOKEN` | No | Enables Discord mode |
| `DISCORD_CHANNEL_ID` | No | Target Discord channel (requires `DISCORD_BOT_TOKEN`) |
| `DISCORD_POLL_INTERVAL_SECONDS` | No | Tune Discord polling interval |
| `DISCORD_MESSAGE_LIMIT` | No | Tune Discord message fetch limit |

Example (Linux / macOS):

```bash
export COMPASS_MODEL_PROVIDER=openai
export OPENAI_API_KEY=sk-...
dotnet run --project samples/Compass.SampleHost
```

---

## 6. Run the Sample Host

```bash
dotnet run --project samples/Compass.SampleHost
```

A REPL will start:

```
Compass SampleHost started. Type a request (or 'quit' to exit):
> summarize this document
  Goal: Summarize (85%), Lane: Communicate
> quit
```

If `DISCORD_BOT_TOKEN` and `DISCORD_CHANNEL_ID` are set, the host switches to Discord mode and polls the configured channel for messages.

---

## 7. Install Plugins (Optional)

1. Build the plugin: `dotnet publish -c Release`
2. Copy the output DLL (and any dependencies) into a `plugins/` folder next to the sample host executable:

   ```bash
   mkdir -p samples/Compass.SampleHost/bin/Debug/net10.0/plugins
   cp path/to/plugin/bin/Release/net10.0/publish/* \
      samples/Compass.SampleHost/bin/Debug/net10.0/plugins/
   ```

3. Run the sample host — it will discover all `ICapabilityModule` and `ISensor` types automatically.

See the [root README](../README.md#how-to-write-a-plugin) for details on writing your own plugin.

---

## Solution Layout

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
│   ├── Compass.SampleHost/              ← Console REPL / Discord demo host
│   └── Compass.SamplePlugin.Basic/      ← Example third-party plugin
└── tests/
    └── UtilityAi.Compass.Tests/         ← xUnit tests
```

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `vendor/UtilityAi` folder is empty or missing | Run `git submodule update --init --recursive` |
| `dotnet: command not found` | Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download) and ensure it is on your `PATH` |
| Build error about target framework `net10.0` | Confirm you have .NET **10** (not an older SDK) installed |
| API key errors at runtime | Double-check the correct `*_API_KEY` variable is set for your chosen `COMPASS_MODEL_PROVIDER` |
| Plugins not discovered | Ensure the DLLs are in a `plugins/` folder next to the running executable |
