namespace UtilityAi.Compass.Tests;

public sealed class MasterVersionBumpWorkflowTests
{
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

        Assert.Contains("if (( patch < 9 )); then", workflow, StringComparison.Ordinal);
        Assert.Contains("if (( minor < 9 )); then", workflow, StringComparison.Ordinal);
        Assert.Contains("major=$((major + 1))", workflow, StringComparison.Ordinal);
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
}
