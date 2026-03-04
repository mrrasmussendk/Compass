# Contributing to UtilityAi.Vitruvian

This guide is for contributors making changes to Vitruvian itself.

## Development setup

```bash
dotnet build UtilityAi.Vitruvian.sln
dotnet test UtilityAi.Vitruvian.sln
```

## Project areas

- `src/UtilityAi.Vitruvian.Abstractions` — enums, facts, interfaces
- `src/UtilityAi.Vitruvian.Runtime` — sensors, modules, strategy, DI
- `src/UtilityAi.Vitruvian.PluginSdk` — plugin attributes and metadata
- `src/UtilityAi.Vitruvian.PluginHost` — plugin loading and registration
- `src/UtilityAi.Vitruvian.Hitl` — human-in-the-loop controls
- `src/UtilityAi.Vitruvian.StandardModules` — built-in reusable modules
- `src/UtilityAi.Vitruvian.WeatherModule` — example weather-focused module
- `src/UtilityAi.Vitruvian.Cli` — CLI host/tooling

## Testing expectations

- Add or update tests in `tests/UtilityAi.Vitruvian.Tests`
- Run targeted tests for changed areas, then full solution tests

## Contribution focus areas

- Governance behavior in `VitruvianGovernedSelectionStrategy`
- Sensor/module composition in runtime DI
- Plugin interoperability through SDK attributes and metadata provider
