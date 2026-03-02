# Using UtilityAi.Compass

This guide is for running Compass as a host application.

## 1) Install and configure

Follow [INSTALL.md](INSTALL.md) for prerequisites, build/test, and provider setup.

## 2) Run the sample host

```bash
dotnet run --project samples/Compass.SampleHost
```

## 3) Understand runtime behavior

For each request, Compass:

1. Uses sensors to classify intent (`GoalTag`) and routing (`Lane`)
2. Collects proposals from built-in and plugin modules
3. Applies governed selection (conflicts, cooldowns, cost/risk, hysteresis)
4. Executes the winning proposal

## 4) Use CLI tooling (optional)

Run the CLI:

```bash
dotnet run --framework net10.0 --project src/UtilityAi.Compass.Cli
```

Common commands:

```bash
compass --setup
compass --list-modules
compass --install-module /absolute/path/MyPlugin.dll
```

If a plugin manifest includes `requiredSecrets`, Compass prompts for missing values during interactive install and fails installation when a required value is not supplied.

Example Gmail prompts you can try in the sample host:

- `read my gmail inbox for unread messages`
- `draft a reply to the latest gmail message`

## 5) Compound requests

Compass handles compound requests automatically when a model client is configured. You can combine multiple independent tasks in a single message:

- `create file u.txt with gold then give me the colors of the rainbow`
- `write hello to greeting.txt and then summarize today's news`
- `send an SMS with the weather forecast then create a log entry`

Each sub-task is routed through the full pipeline, so the right module handles each part — no module needs special compound-request awareness. See [COMPOUND-REQUESTS.md](COMPOUND-REQUESTS.md) for architecture details.
