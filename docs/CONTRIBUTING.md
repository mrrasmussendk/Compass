# Contributing to UtilityAi.Compass

This guide is for contributors making changes to Compass itself.

## Development setup

```bash
dotnet build UtilityAi.Compass.sln
dotnet test UtilityAi.Compass.sln
```

## Project areas

- `src/UtilityAi.Compass.Abstractions` — enums, facts, interfaces
- `src/UtilityAi.Compass.Runtime` — sensors, modules, strategy, DI
- `src/UtilityAi.Compass.PluginSdk` — plugin attributes and metadata
- `src/UtilityAi.Compass.PluginHost` — plugin loading and registration
- `src/UtilityAi.Compass.Hitl` — human-in-the-loop controls

## Testing expectations

- Add or update tests in `tests/UtilityAi.Compass.Tests`
- Run targeted tests for changed areas, then full solution tests

## Cross-repository agent trigger pipeline

`build-pack` now triggers `trigger-external-agent` after a successful run.

To enable dispatching to your own repository:

- Set repository variable `AGENT_REPOSITORY` to `owner/repo`
- (Optional) Set repository variable `AGENT_EVENT_TYPE` (defaults to `compass-build-completed`)
- Add secret `AGENT_REPO_TOKEN` with permission to dispatch events to that repository

## Contribution focus areas

- Governance behavior in `CompassGovernedSelectionStrategy`
- Sensor/module composition in runtime DI
- Plugin interoperability through SDK attributes and metadata provider
