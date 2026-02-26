namespace UtilityAi.Compass.Abstractions.Facts;

/// <summary>Fact published by the CLI intent sensor when a command-line operation is detected.</summary>
/// <param name="Verb">The detected <see cref="CliVerb"/> (Read, Write, or Update).</param>
/// <param name="Target">Optional target resource or entity for the CLI operation.</param>
/// <param name="Confidence">Confidence score in the range [0, 1].</param>
public sealed record CliIntent(CliVerb Verb, string? Target, double Confidence);
