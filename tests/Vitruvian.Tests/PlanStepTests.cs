using VitruvianAbstractions;
using VitruvianAbstractions.Planning;
using Xunit;

namespace VitruvianTests;

public sealed class PlanStepTests
{
    [Fact]
    public void Complexity_DefaultsToNull()
    {
        var step = new PlanStep("s1", "test", "desc", "input", []);
        Assert.Null(step.Complexity);
    }

    [Fact]
    public void Complexity_CanBeSet()
    {
        var step = new PlanStep("s1", "test", "desc", "input", [], Complexity.High);
        Assert.Equal(Complexity.High, step.Complexity);
    }
}
