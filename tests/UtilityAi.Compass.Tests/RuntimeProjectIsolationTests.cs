namespace UtilityAi.Compass.Tests;

public class RuntimeProjectIsolationTests
{
    [Fact]
    public void RuntimeProject_DoesNotReferenceStandardModules()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var runtimeProject = Path.Combine(repoRoot, "src", "UtilityAi.Compass.Runtime", "UtilityAi.Compass.Runtime.csproj");

        var csproj = File.ReadAllText(runtimeProject);

        Assert.DoesNotContain("UtilityAi.Compass.StandardModules", csproj, StringComparison.Ordinal);
    }
}
