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

    [Fact]
    public void Precondition_DefaultsToNull()
    {
        var step = new PlanStep("s1", "test", "desc", "input", []);
        Assert.Null(step.Precondition);
    }

    [Fact]
    public void Precondition_CanBeSet()
    {
        var step = new PlanStep("s1", "test", "desc", "input", [], Precondition: "file must exist");
        Assert.Equal("file must exist", step.Precondition);
    }

    [Fact]
    public void Postcondition_DefaultsToNull()
    {
        var step = new PlanStep("s1", "test", "desc", "input", []);
        Assert.Null(step.Postcondition);
    }

    [Fact]
    public void Postcondition_CanBeSet()
    {
        var step = new PlanStep("s1", "test", "desc", "input", [], Postcondition: "success");
        Assert.Equal("success", step.Postcondition);
    }

    [Fact]
    public void FallbackStepId_DefaultsToNull()
    {
        var step = new PlanStep("s1", "test", "desc", "input", []);
        Assert.Null(step.FallbackStepId);
    }

    [Fact]
    public void FallbackStepId_CanBeSet()
    {
        var step = new PlanStep("s1", "test", "desc", "input", [], FallbackStepId: "s1-fb");
        Assert.Equal("s1-fb", step.FallbackStepId);
    }

    [Fact]
    public void WasFallback_DefaultsToFalse()
    {
        var result = new PlanStepResult("s1", "test", true, "ok", DateTimeOffset.UtcNow, TimeSpan.Zero);
        Assert.False(result.WasFallback);
    }

    [Fact]
    public void WasFallback_CanBeSetToTrue()
    {
        var result = new PlanStepResult("s1", "test", true, "ok", DateTimeOffset.UtcNow, TimeSpan.Zero, WasFallback: true);
        Assert.True(result.WasFallback);
    }
}
