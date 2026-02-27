using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Abstractions.Workflow;
using UtilityAi.Compass.PluginHost;
using UtilityAi.Consideration;
using System.Reflection;

namespace UtilityAi.Compass.Tests;

public class WorkflowPluginLoaderTests
{
    [Fact]
    public void DiscoverWorkflowModules_FindsImplementationsInAssembly()
    {
        var loader = new PluginLoader();
        loader.LoadAssembly(typeof(TestWorkflowModule).Assembly);

        var modules = loader.DiscoverWorkflowModules().ToList();
        Assert.Single(modules);
        Assert.IsType<TestWorkflowModule>(modules[0]);
    }

    [Fact]
    public void DiscoverWorkflowModules_ReturnsEmpty_WhenNoModules()
    {
        var loader = new PluginLoader();
        // Don't load any assembly
        var modules = loader.DiscoverWorkflowModules().ToList();
        Assert.Empty(modules);
    }

    [Fact]
    public void DiscoveredWorkflowModule_HasValidDefinition()
    {
        var loader = new PluginLoader();
        loader.LoadAssembly(typeof(TestWorkflowModule).Assembly);

        var module = loader.DiscoverWorkflowModules().First();
        var definition = module.Define();

        Assert.Equal("test-workflow", definition.WorkflowId);
        Assert.Equal("Test Workflow", definition.DisplayName);
        Assert.Contains(GoalTag.Execute, definition.Goals);
        Assert.Single(definition.Steps);
    }
}

/// <summary>Test workflow module used by discovery tests.</summary>
public sealed class TestWorkflowModule : IWorkflowModule
{
    public WorkflowDefinition Define() => new(
        WorkflowId: "test-workflow",
        DisplayName: "Test Workflow",
        Goals: [GoalTag.Execute],
        Lanes: [Lane.Execute],
        Steps: [new StepDefinition("step-1", "Test Step", [], [])]);

    public IEnumerable<Proposal> ProposeStart(UtilityAi.Utils.Runtime rt)
        => [];

    public IEnumerable<Proposal> ProposeSteps(UtilityAi.Utils.Runtime rt, ActiveWorkflow active)
        => [];

    public IEnumerable<Proposal> ProposeRepair(UtilityAi.Utils.Runtime rt, ActiveWorkflow active, RepairDirective directive)
        => [];
}
