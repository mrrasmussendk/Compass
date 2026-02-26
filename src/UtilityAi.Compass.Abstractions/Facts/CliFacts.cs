namespace UtilityAi.Compass.Abstractions.Facts;

public sealed record CliIntent(CliVerb Verb, string? Target, double Confidence);
