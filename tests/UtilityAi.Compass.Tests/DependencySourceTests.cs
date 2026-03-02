namespace UtilityAi.Compass.Tests;

public class DependencySourceTests
{
    [Fact]
    public void Repository_DoesNotTrackUtilityAiAsVendorSubmodule()
    {
        var repoRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var gitmodulesPath = Path.Combine(repoRoot, ".gitmodules");

        if (!File.Exists(gitmodulesPath))
            return;

        var gitmodules = File.ReadAllText(gitmodulesPath);
        Assert.DoesNotContain("vendor/UtilityAi", gitmodules, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "UtilityAi.Compass.sln")))
            current = current.Parent;

        return current?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
