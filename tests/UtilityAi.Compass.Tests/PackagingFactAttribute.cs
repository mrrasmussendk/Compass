namespace UtilityAi.Compass.Tests;

/// <summary>
/// Marks a test as a packaging integration test that spawns external processes
/// (e.g. <c>dotnet pack</c>). These tests are skipped by default because they
/// take several minutes. Set the <c>COMPASS_PACKAGING_TESTS</c> environment
/// variable to <c>1</c> to enable them.
/// </summary>
public sealed class PackagingFactAttribute : FactAttribute
{
    public PackagingFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("COMPASS_PACKAGING_TESTS"), "1", StringComparison.Ordinal))
            Skip = "Packaging tests are slow and disabled by default. Set COMPASS_PACKAGING_TESTS=1 to run.";
    }
}
