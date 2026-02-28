# Extending UtilityAi.Compass

This guide is for plugin authors extending Compass with new capabilities.

## 1) Create a plugin project

1. Create a `net10.0` class library
2. Reference `UtilityAi.Compass.PluginSdk`
3. Implement `ICapabilityModule` (optionally `ISensor` / `ICliAction`)

## 2) Annotate capability metadata

Use SDK attributes so governance can route and score proposals:

- `CompassCapability`
- `CompassGoals`
- `CompassLane`
- `CompassCost`
- `CompassRisk`
- `CompassCooldown`
- `CompassConflicts`

## 3) Build and install

Build/publish the plugin and place outputs in a `plugins/` folder next to the host executable, or install via CLI:

```bash
compass --install-module /absolute/path/MyPlugin.dll
```

## 4) Example

See:

- `samples/Compass.SamplePlugin.Basic`
- `samples/Compass.SamplePlugin.OpenAi`
- Plugin example in the root [README.md](../README.md#example-capability-module)
