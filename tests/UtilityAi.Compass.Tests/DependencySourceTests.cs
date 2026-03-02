namespace UtilityAi.Compass.Tests;

public class DependencySourceTests
{
    [Fact]
    public void Repository_DoesNotTrackUtilityAiAsVendorSubmodule()
    {
        var repoRoot = GetRepositoryRoot(AppContext.BaseDirectory);
        var gitmodulesPath = Path.Combine(repoRoot, ".gitmodules");

        Assert.True(
            !File.Exists(gitmodulesPath) ||
            !File.ReadAllText(gitmodulesPath).Contains("vendor/UtilityAi", StringComparison.Ordinal));
    }

    private static string GetRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "UtilityAi.Compass.sln")))
            current = current.Parent;

        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
