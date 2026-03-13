using VitruvianAbstractions.Interfaces;
using VitruvianAbstractions.Planning;
using VitruvianRuntime.Planning;
using Xunit;

namespace VitruvianTests;

public sealed class GoapPlannerTests
{
    private sealed class StubModelClient : IModelClient
    {
        private readonly string _response;

        public StubModelClient(string response) => _response = response;

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ModelResponse { Text = _response });

        public Task<string> CompleteAsync(string systemMessage, string userMessage, IReadOnlyList<ModelTool>? tools = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);
    }

    [Fact]
    public async Task CreatePlanAsync_NoModules_ReturnsSingleStepWithEmptyDomain()
    {
        var planner = new GoapPlanner();

        var plan = await planner.CreatePlanAsync("do something", CancellationToken.None);

        Assert.Single(plan.Steps);
        Assert.Equal("", plan.Steps[0].ModuleDomain);
    }

    [Fact]
    public async Task CreatePlanAsync_NoModelClient_FallsBackToKeywordMatching()
    {
        var planner = new GoapPlanner();
        planner.RegisterModule("file-ops", "File operations for reading and writing files");

        var plan = await planner.CreatePlanAsync("read a file", CancellationToken.None);

        Assert.Single(plan.Steps);
        Assert.Equal("file-ops", plan.Steps[0].ModuleDomain);
    }

    [Fact]
    public async Task CreatePlanAsync_LlmReturnsSingleStep_ParsesCorrectly()
    {
        var json = """[{"step_id":"s1","module":"conversation","description":"Answer the question","input":"What is 2+2?","depends_on":[]}]""";
        var planner = new GoapPlanner(new StubModelClient(json));
        planner.RegisterModule("conversation", "General conversation");

        var plan = await planner.CreatePlanAsync("What is 2+2?", CancellationToken.None);

        Assert.Single(plan.Steps);
        Assert.Equal("s1", plan.Steps[0].StepId);
        Assert.Equal("conversation", plan.Steps[0].ModuleDomain);
        Assert.Empty(plan.Steps[0].DependsOn);
    }

    [Fact]
    public async Task CreatePlanAsync_LlmReturnsMultiStep_ParsesDependencies()
    {
        var json = """
        [
            {"step_id":"s1","module":"file-ops","description":"Read the file","input":"read notes.txt","depends_on":[]},
            {"step_id":"s2","module":"conversation","description":"Summarize content","input":"summarize the file content","depends_on":["s1"]}
        ]
        """;
        var planner = new GoapPlanner(new StubModelClient(json));
        planner.RegisterModule("file-ops", "File operations");
        planner.RegisterModule("conversation", "General conversation");

        var plan = await planner.CreatePlanAsync("read notes.txt then summarize it", CancellationToken.None);

        Assert.Equal(2, plan.Steps.Count);
        Assert.Empty(plan.Steps[0].DependsOn);
        Assert.Single(plan.Steps[1].DependsOn);
        Assert.Equal("s1", plan.Steps[1].DependsOn[0]);
    }

    [Fact]
    public async Task CreatePlanAsync_LlmReturnsParallelSteps_NoDepsBetweenThem()
    {
        var json = """
        [
            {"step_id":"s1","module":"file-ops","description":"Read file A","input":"read a.txt","depends_on":[]},
            {"step_id":"s2","module":"file-ops","description":"Read file B","input":"read b.txt","depends_on":[]}
        ]
        """;
        var planner = new GoapPlanner(new StubModelClient(json));
        planner.RegisterModule("file-ops", "File operations");

        var plan = await planner.CreatePlanAsync("read a.txt and b.txt", CancellationToken.None);

        Assert.Equal(2, plan.Steps.Count);
        Assert.All(plan.Steps, step => Assert.Empty(step.DependsOn));
    }

    [Fact]
    public async Task CreatePlanAsync_LlmReturnsInvalidJson_FallsBackToSingleStep()
    {
        var planner = new GoapPlanner(new StubModelClient("not valid json at all"));
        planner.RegisterModule("conversation", "General conversation and questions");

        var plan = await planner.CreatePlanAsync("hello", CancellationToken.None);

        Assert.Single(plan.Steps);
    }

    [Fact]
    public async Task CreatePlanAsync_LlmReturnsUnknownModule_SkipsIt()
    {
        var json = """[{"step_id":"s1","module":"unknown-module","description":"Do something","input":"test","depends_on":[]}]""";
        var planner = new GoapPlanner(new StubModelClient(json));
        planner.RegisterModule("conversation", "General conversation");

        var plan = await planner.CreatePlanAsync("test", CancellationToken.None);

        // Unknown module is skipped, falls back to single step
        Assert.Single(plan.Steps);
        Assert.Equal("conversation", plan.Steps[0].ModuleDomain);
    }

    [Fact]
    public async Task CreatePlanAsync_LlmReturnsMarkdownWrappedJson_ParsesCorrectly()
    {
        var json = """
        ```json
        [{"step_id":"s1","module":"conversation","description":"Answer","input":"hello","depends_on":[]}]
        ```
        """;
        var planner = new GoapPlanner(new StubModelClient(json));
        planner.RegisterModule("conversation", "General conversation");

        var plan = await planner.CreatePlanAsync("hello", CancellationToken.None);

        Assert.Single(plan.Steps);
        Assert.Equal("conversation", plan.Steps[0].ModuleDomain);
    }

    [Fact]
    public async Task CreatePlanAsync_PlanIdIsUnique()
    {
        var planner = new GoapPlanner();
        planner.RegisterModule("conversation", "General conversation");

        var plan1 = await planner.CreatePlanAsync("hello", CancellationToken.None);
        var plan2 = await planner.CreatePlanAsync("world", CancellationToken.None);

        Assert.NotEqual(plan1.PlanId, plan2.PlanId);
    }

    [Fact]
    public async Task CreatePlanAsync_PreservesOriginalRequest()
    {
        var planner = new GoapPlanner();
        planner.RegisterModule("conversation", "General conversation");

        var plan = await planner.CreatePlanAsync("my original request", CancellationToken.None);

        Assert.Equal("my original request", plan.OriginalRequest);
    }

    [Fact]
    public void UnregisterModule_WithExistingDomain_ReturnsTrue()
    {
        var planner = new GoapPlanner();
        planner.RegisterModule("file-ops", "File operations");

        var removed = planner.UnregisterModule("file-ops");

        Assert.True(removed);
    }

    [Fact]
    public void UnregisterModule_WithNonExistentDomain_ReturnsFalse()
    {
        var planner = new GoapPlanner();

        var removed = planner.UnregisterModule("does-not-exist");

        Assert.False(removed);
    }

    [Fact]
    public async Task UnregisterModule_RemovedModuleIsNoLongerPlanned()
    {
        var planner = new GoapPlanner();
        planner.RegisterModule("file-ops", "File operations for reading and writing files");
        planner.RegisterModule("conversation", "General conversation and questions");

        planner.UnregisterModule("file-ops");

        var plan = await planner.CreatePlanAsync("read a file", CancellationToken.None);
        Assert.Single(plan.Steps);
        Assert.Equal("conversation", plan.Steps[0].ModuleDomain);
    }

    [Fact]
    public async Task UnregisterModule_AllModulesRemoved_ReturnsSingleStepWithEmptyDomain()
    {
        var planner = new GoapPlanner();
        planner.RegisterModule("only-module", "The only module");

        planner.UnregisterModule("only-module");

        var plan = await planner.CreatePlanAsync("test", CancellationToken.None);
        Assert.Single(plan.Steps);
        Assert.Equal("", plan.Steps[0].ModuleDomain);
    }

    [Fact]
    public async Task CreatePlanAsync_LlmReturnsComplexity_ParsesCorrectly()
    {
        var json = """[{"step_id":"s1","module":"conversation","description":"Answer","input":"hello","depends_on":[],"complexity":"high"}]""";
        var planner = new GoapPlanner(new StubModelClient(json));
        planner.RegisterModule("conversation", "General conversation");

        var plan = await planner.CreatePlanAsync("hello", CancellationToken.None);

        Assert.Single(plan.Steps);
        Assert.Equal(VitruvianAbstractions.Complexity.High, plan.Steps[0].Complexity);
    }

    [Fact]
    public async Task CreatePlanAsync_LlmReturnsComplexityCaseInsensitive_ParsesCorrectly()
    {
        var json = """[{"step_id":"s1","module":"conversation","description":"Answer","input":"hello","depends_on":[],"complexity":"Medium"}]""";
        var planner = new GoapPlanner(new StubModelClient(json));
        planner.RegisterModule("conversation", "General conversation");

        var plan = await planner.CreatePlanAsync("hello", CancellationToken.None);

        Assert.Single(plan.Steps);
        Assert.Equal(VitruvianAbstractions.Complexity.Medium, plan.Steps[0].Complexity);
    }

    [Fact]
    public async Task CreatePlanAsync_LlmOmitsComplexity_DefaultsToNull()
    {
        var json = """[{"step_id":"s1","module":"conversation","description":"Answer","input":"hello","depends_on":[]}]""";
        var planner = new GoapPlanner(new StubModelClient(json));
        planner.RegisterModule("conversation", "General conversation");

        var plan = await planner.CreatePlanAsync("hello", CancellationToken.None);

        Assert.Single(plan.Steps);
        Assert.Null(plan.Steps[0].Complexity);
    }

    [Fact]
    public async Task CreatePlanAsync_LlmReturnsInvalidComplexity_DefaultsToNull()
    {
        var json = """[{"step_id":"s1","module":"conversation","description":"Answer","input":"hello","depends_on":[],"complexity":"extreme"}]""";
        var planner = new GoapPlanner(new StubModelClient(json));
        planner.RegisterModule("conversation", "General conversation");

        var plan = await planner.CreatePlanAsync("hello", CancellationToken.None);

        Assert.Single(plan.Steps);
        Assert.Null(plan.Steps[0].Complexity);
    }

    [Fact]
    public async Task CreatePlanAsync_FallbackPlan_ComplexityIsNull()
    {
        var planner = new GoapPlanner();
        planner.RegisterModule("conversation", "General conversation");

        var plan = await planner.CreatePlanAsync("hello", CancellationToken.None);

        Assert.Single(plan.Steps);
        Assert.Null(plan.Steps[0].Complexity);
    }

    [Fact]
    public async Task CreatePlanAsync_MultiStepWithMixedComplexity_ParsesEach()
    {
        var json = """
        [
            {"step_id":"s1","module":"file-ops","description":"Read file","input":"read data.txt","depends_on":[],"complexity":"low"},
            {"step_id":"s2","module":"conversation","description":"Analyze content","input":"analyze the data","depends_on":["s1"],"complexity":"high"}
        ]
        """;
        var planner = new GoapPlanner(new StubModelClient(json));
        planner.RegisterModule("file-ops", "File operations");
        planner.RegisterModule("conversation", "General conversation");

        var plan = await planner.CreatePlanAsync("read and analyze data.txt", CancellationToken.None);

        Assert.Equal(2, plan.Steps.Count);
        Assert.Equal(VitruvianAbstractions.Complexity.Low, plan.Steps[0].Complexity);
        Assert.Equal(VitruvianAbstractions.Complexity.High, plan.Steps[1].Complexity);
    }

    [Fact]
    public void BuildPlannerPrompt_DefaultTemplate_ContainsModuleList()
    {
        var planner = new GoapPlanner();
        planner.RegisterModule("file-ops", "File operations");
        planner.RegisterModule("conversation", "General conversation");

        var prompt = planner.BuildPlannerPrompt();

        Assert.Contains("file-ops: File operations", prompt);
        Assert.Contains("conversation: General conversation", prompt);
        Assert.Contains("GOAP", prompt);
    }

    [Fact]
    public void BuildPlannerPrompt_CustomTemplate_ReplacesModulesPlaceholder()
    {
        var planner = new GoapPlanner();
        planner.RegisterModule("file-ops", "File operations");
        planner.PlannerPromptTemplate = "Custom planner prompt.\n\nModules:\n{modules}\n\nEnd.";

        var prompt = planner.BuildPlannerPrompt();

        Assert.Contains("Custom planner prompt.", prompt);
        Assert.Contains("file-ops: File operations", prompt);
        Assert.Contains("End.", prompt);
        Assert.DoesNotContain("{modules}", prompt);
    }

    [Fact]
    public void BuildPlannerPrompt_CustomTemplate_NullFallsBackToDefault()
    {
        var planner = new GoapPlanner();
        planner.RegisterModule("conversation", "General conversation");
        planner.PlannerPromptTemplate = null;

        var prompt = planner.BuildPlannerPrompt();

        Assert.Contains("GOAP", prompt);
        Assert.Contains("conversation: General conversation", prompt);
    }
}
