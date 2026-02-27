namespace UtilityAi.Compass.Tests;

public sealed class MasterVersionBumpWorkflowTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.4")]
    [InlineData("1.2.9", "1.3.0")]
    [InlineData("1.9.9", "2.0.0")]
    public void BumpSemVer_RollsOverAsExpected(string currentVersion, string expectedVersion)
    {
        var next = BumpSemVer(currentVersion);

        Assert.Equal(expectedVersion, next);
    }

    [Fact]
    public void MasterVersionBumpWorkflow_RunsOnlyOnMasterPushes()
    {
        var workflow = File.ReadAllText(GetWorkflowPath());

        Assert.Contains("branches: [\"master\"]", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pull_request:", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void MasterVersionBumpWorkflow_ContainsExpectedRolloverLogic()
    {
        var workflow = File.ReadAllText(GetWorkflowPath());

        Assert.Contains("if patch < 9:", workflow, StringComparison.Ordinal);
        Assert.Contains("if minor < 9:", workflow, StringComparison.Ordinal);
        Assert.Contains("major += 1", workflow, StringComparison.Ordinal);
    }

    private static string GetWorkflowPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".github", "workflows", "master-version-bump.yml");
            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate master-version-bump.yml.");
    }

    private static string BumpSemVer(string version)
    {
        var parts = version.Split('.');
        var major = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        var minor = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        var patch = int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);

        if (patch < 9)
        {
            patch++;
        }
        else
        {
            patch = 0;
            if (minor < 9)
            {
                minor++;
            }
            else
            {
                minor = 0;
                major++;
            }
        }

        return $"{major}.{minor}.{patch}";
    }
}
