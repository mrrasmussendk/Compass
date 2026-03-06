# Vitruvian Agent Runtime — Documentation

Welcome to the **Vitruvian Agent Runtime** documentation. This folder contains detailed guides for every aspect of the framework. The root [README](../README.md) provides a quick-start overview; these pages go deeper.

---

## Quick Navigation

| I want to … | Start here |
|---|---|
| **Install and run** Vitruvian | [Installation](INSTALL.md) → [Using Vitruvian](USING.md) |
| **Understand the architecture** | [Architecture](ARCHITECTURE.md) |
| **See available built-in modules** | [Modules](MODULES.md) |
| **Build a plugin module** | [Extending Vitruvian](EXTENDING.md) |
| **Understand governance & scoring** | [Governance](GOVERNANCE.md) |
| **Review the security model** | [Security](SECURITY.md) |
| **Configure policies** | [Policy](POLICY.md) |
| **Run operational commands** (audit, replay, doctor) | [Operations](OPERATIONS.md) |
| **Understand compound requests** | [Compound Requests](COMPOUND-REQUESTS.md) |
| **Contribute to the project** | [Contributing](CONTRIBUTING.md) |

---

## Guides

### Getting Started

- **[Installation](INSTALL.md)** — Prerequisites, building, guided & manual setup, plugin installation, troubleshooting.
- **[Using Vitruvian](USING.md)** — Running the CLI, runtime behaviour, available commands, compound requests.
- **[Modules](MODULES.md)** — Built-in standard modules and Google MCP modules (Gmail, Google Drive, Google Calendar).

### Architecture & Design

- **[Architecture](ARCHITECTURE.md)** — GOAP pipeline, key components (`IVitruvianModule`, `GoapPlanner`, `PlanExecutor`, `ModuleRouter`), execution flow, planning types.
- **[Compound Requests](COMPOUND-REQUESTS.md)** — How multi-intent messages are detected, decomposed via LLM, and executed independently through the full pipeline.

### Extending Vitruvian

- **[Extending Vitruvian](EXTENDING.md)** — Writing a plugin module: project setup, SDK attributes, permissions, API key declarations, build & install.

### Governance, Security & Operations

- **[Governance](GOVERNANCE.md)** — Proposal generation → filtering → conflict/cooldown handling → cost/risk scoring → execution. Scoring formula, hysteresis, explainability commands.
- **[Security](SECURITY.md)** — Four-layer security model: permissions (`[RequiresPermission]`), HITL approval (`IApprovalGate`), module sandboxing (`SandboxedModuleRunner`), installation controls (manifest + signing).
- **[Policy](POLICY.md)** — Policy validation, the `EnterpriseSafe` default, and the `policy explain` command.
- **[Operations](OPERATIONS.md)** — Audit listing/inspection, replay, and the `doctor` diagnostic tool.

### Contributing

- **[Contributing](CONTRIBUTING.md)** — Development setup, project areas, testing expectations, contribution focus areas.

---

## Repository Layout

```
Vitruvian.sln
├── src/
│   ├── Vitruvian.Abstractions/      ← Core interfaces, enums, facts, planning types
│   ├── Vitruvian.Runtime/           ← GoapPlanner, PlanExecutor, ModuleRouter, DI
│   ├── Vitruvian.PluginSdk/         ← SDK attributes for module metadata
│   ├── Vitruvian.PluginHost/        ← Plugin loader (AssemblyLoadContext), sandboxing
│   ├── Vitruvian.Hitl/              ← ConsoleApprovalGate, HITL facts
│   ├── Vitruvian.StandardModules/   ← Built-in modules (File, Conversation, Web, …)
│   ├── Vitruvian.WeatherModule/     ← Example standalone module
│   └── Vitruvian.Cli/               ← CLI entry point, RequestProcessor
├── modules/
│   ├── Vitruvian.GmailModule/       ← Gmail MCP module
│   ├── Vitruvian.GoogleDriveModule/ ← Google Drive MCP module
│   └── Vitruvian.GoogleCalendarModule/ ← Google Calendar MCP module
├── tests/
│   └── Vitruvian.Tests/             ← xUnit tests
├── docs/                            ← You are here
└── scripts/                         ← Guided setup (install.sh / install.ps1)
```

---

## Key Concepts

| Concept | Description |
|---------|-------------|
| **`IVitruvianModule`** | The single interface every capability implements. Exposes `Domain`, `Description`, and `ExecuteAsync`. |
| **`GoapPlanner`** | Builds an `ExecutionPlan` (a DAG of `PlanStep` nodes) from a user request and the registered modules. |
| **`PlanExecutor`** | Runs plan steps in dependency waves with caching, HITL gating, and context injection. |
| **`GoalTag`** | Classifies user intent: `Answer`, `Clarify`, `Summarize`, `Execute`, `Approve`, `Stop`. |
| **`Lane`** | Routes proposals: `Interpret`, `Plan`, `Execute`, `Communicate`, `Safety`, `Housekeeping`. |
| **`SideEffectLevel`** | Classifies action impact: `ReadOnly`, `Write`, `Destructive`. |
| **`IApprovalGate`** | Human-in-the-loop approval interface. Default implementation: `ConsoleApprovalGate`. |
| **`ISandboxPolicy`** | Resource limits for untrusted module execution (CPU, memory, wall time, file/network/process access). |
