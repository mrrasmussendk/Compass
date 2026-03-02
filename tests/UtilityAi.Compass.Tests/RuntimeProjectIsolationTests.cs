namespace UtilityAi.Compass.Tests;

public class RuntimeProjectIsolationTests
{
    [Fact]
    public void RuntimeProject_DoesNotReferenceStandardModules()
    {
        var repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var runtimeProject = Path.Combine(repoRoot, "src", "UtilityAi.Compass.Runtime", "UtilityAi.Compass.Runtime.csproj");

        var csproj = File.ReadAllText(runtimeProject);

        Assert.DoesNotContain("UtilityAi.Compass.StandardModules", csproj, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "UtilityAi.Compass.sln")))
            current = current.Parent;

        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
