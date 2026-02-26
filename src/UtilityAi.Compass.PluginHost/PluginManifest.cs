namespace UtilityAi.Compass.PluginHost;

public sealed record PluginManifest(
    string AssemblyName,
    string? Version,
    IReadOnlyList<string> ModuleTypes,
    IReadOnlyList<string> SensorTypes,
    IReadOnlyList<string> SinkTypes
);
