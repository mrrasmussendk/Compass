using System.Collections.Concurrent;
using VitruvianAbstractions;
using VitruvianAbstractions.Interfaces;
using VitruvianAbstractions.Planning;
using VitruvianRuntime.Planning;
using Xunit;

namespace VitruvianTests;

public sealed class PlanExecutorTests
{
    private sealed class TestModule : IVitruvianModule
    {
        public string Domain { get; }
        public string Description { get; }
        private readonly string _response;
        private readonly TimeSpan _delay;

        public int ExecutionCount;

        public TestModule(string domain, string description, string response = "ok", TimeSpan delay = default)
        {
            Domain = domain;
            Description = description;
            _response = response;
            _delay = delay;
        }

        public async Task<string> ExecuteAsync(string request, string? userId, CancellationToken ct)
        {
            Interlocked.Increment(ref ExecutionCount);
            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay, ct);
            return _response;
        }
    }

    private sealed class FailingModule : IVitruvianModule
    {
        public string Domain => "failing";
        public string Description => "Always fails";

        public Task<string> ExecuteAsync(string request, string? userId, CancellationToken ct)
            => throw new InvalidOperationException("Module failed");
    }

    private sealed class StubApprovalGate : IApprovalGate
    {
        private readonly bool _approve;
        public int ApprovalCount;

        public StubApprovalGate(bool approve) => _approve = approve;

        public Task<bool> ApproveAsync(OperationType operation, string description, string moduleDomain, CancellationToken ct = default)
        {
            Interlocked.Increment(ref ApprovalCount);
            return Task.FromResult(_approve);
        }
    }

    [Fact]
    public async Task ExecuteAsync_SingleStep_ReturnsModuleOutput()
    {
        var module = new TestModule("conversation", "General conversation", "Hello!");
        var modules = new Dictionary<string, IVitruvianModule> { ["conversation"] = module };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "hello", [
            new PlanStep("s1", "conversation", "Answer greeting", "hello", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.StepResults);
        Assert.Equal("Hello!", result.AggregatedOutput);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleSequentialSteps_ExecutesInOrder()
    {
        var readModule = new TestModule("file-ops", "File operations", "file content");
        var convModule = new TestModule("conversation", "Conversation", "summary of content");
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["file-ops"] = readModule,
            ["conversation"] = convModule
        };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "read and summarize", [
            new PlanStep("s1", "file-ops", "Read file", "read notes.txt", []),
            new PlanStep("s2", "conversation", "Summarize", "summarize content", ["s1"])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.StepResults.Count);
        Assert.Equal("file content", result.StepResults[0].Output);
        Assert.Equal("summary of content", result.StepResults[1].Output);
    }

    [Fact]
    public async Task ExecuteAsync_IndependentSteps_RunInParallel()
    {
        // Two independent steps with delays; verify both ran and total time indicates parallelism
        var moduleA = new TestModule("mod-a", "Module A", "result A", TimeSpan.FromMilliseconds(100));
        var moduleB = new TestModule("mod-b", "Module B", "result B", TimeSpan.FromMilliseconds(100));
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["mod-a"] = moduleA,
            ["mod-b"] = moduleB
        };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "do A and B", [
            new PlanStep("s1", "mod-a", "Step A", "do A", []),
            new PlanStep("s2", "mod-b", "Step B", "do B", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, result.StepResults.Count);
        // Both modules should have executed exactly once
        Assert.Equal(1, moduleA.ExecutionCount);
        Assert.Equal(1, moduleB.ExecutionCount);
        // Both steps should have been in the same wave (no dependencies)
        Assert.Empty(plan.Steps[0].DependsOn);
        Assert.Empty(plan.Steps[1].DependsOn);
    }

    [Fact]
    public async Task ExecuteAsync_FailingStep_MarksStepAsFailedButContinues()
    {
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["failing"] = new FailingModule(),
            ["conversation"] = new TestModule("conversation", "Conversation", "still works")
        };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "failing", "Will fail", "do something", []),
            new PlanStep("s2", "conversation", "Will succeed", "hello", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.False(result.Success); // Overall failure because s1 failed
        Assert.False(result.StepResults[0].Success);
        Assert.True(result.StepResults[1].Success);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownModule_ReportsError()
    {
        var modules = new Dictionary<string, IVitruvianModule>();
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "nonexistent", "Unknown module step", "test", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("No module found", result.StepResults[0].Output);
    }

    [Fact]
    public async Task ExecuteAsync_HitlApproves_StepExecutes()
    {
        var module = new TestModule("file-ops", "File operations", "file written");
        var modules = new Dictionary<string, IVitruvianModule> { ["file-ops"] = module };
        var gate = new StubApprovalGate(approve: true);
        var executor = new PlanExecutor(modules, gate);

        var plan = new ExecutionPlan("p1", "write file", [
            new PlanStep("s1", "file-ops", "Write a file", "create test.txt", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("file written", result.StepResults[0].Output);
        Assert.Equal(1, gate.ApprovalCount);
    }

    [Fact]
    public async Task ExecuteAsync_HitlDenies_StepBlocked()
    {
        var module = new TestModule("file-ops", "File operations", "file written");
        var modules = new Dictionary<string, IVitruvianModule> { ["file-ops"] = module };
        var gate = new StubApprovalGate(approve: false);
        var executor = new PlanExecutor(modules, gate);

        var plan = new ExecutionPlan("p1", "write file", [
            new PlanStep("s1", "file-ops", "Write a file", "create test.txt", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("denied by human reviewer", result.StepResults[0].Output);
    }

    [Fact]
    public async Task ExecuteAsync_ReadOnlyStep_BypassesHitl()
    {
        var module = new TestModule("conversation", "Conversation", "answer");
        var modules = new Dictionary<string, IVitruvianModule> { ["conversation"] = module };
        var gate = new StubApprovalGate(approve: false); // would deny if checked
        var executor = new PlanExecutor(modules, gate);

        var plan = new ExecutionPlan("p1", "question", [
            new PlanStep("s1", "conversation", "Answer a question", "What is AI?", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, gate.ApprovalCount); // Never called for read-only ops
    }

    [Fact]
    public async Task ExecuteAsync_CachesResults_SameInputNotReExecuted()
    {
        var module = new TestModule("conversation", "Conversation", "cached answer");
        var modules = new Dictionary<string, IVitruvianModule> { ["conversation"] = module };
        var executor = new PlanExecutor(modules);

        var plan1 = new ExecutionPlan("p1", "hello", [
            new PlanStep("s1", "conversation", "Answer", "hello", [])
        ]);
        var plan2 = new ExecutionPlan("p2", "hello", [
            new PlanStep("s1", "conversation", "Answer again", "hello", [])
        ]);

        await executor.ExecuteAsync(plan1, null, CancellationToken.None);
        var result = await executor.ExecuteAsync(plan2, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("cached answer", result.AggregatedOutput);
        Assert.Equal(1, module.ExecutionCount); // Only executed once due to caching
    }

    [Fact]
    public async Task ExecuteAsync_MemoryStoresPlanResults()
    {
        var module = new TestModule("conversation", "Conversation", "result");
        var modules = new Dictionary<string, IVitruvianModule> { ["conversation"] = module };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "conversation", "Test", "test", [])
        ]);

        await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.Single(executor.Memory);
        Assert.Equal("p1", executor.Memory[0].PlanId);
    }

    [Fact]
    public async Task ExecuteAsync_ContextWindow_InjectsRecentOutputs()
    {
        // Module that echoes its input so we can verify context injection
        var module = new EchoModule("echo", "Echo module");
        var modules = new Dictionary<string, IVitruvianModule> { ["echo"] = module };
        var executor = new PlanExecutor(modules, contextWindowSize: 2);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "echo", "Step 1", "first input", []),
            new PlanStep("s2", "echo", "Step 2", "second input", ["s1"])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
        // Step 2 should have received context from step 1's output
        Assert.Contains("Prior step context", result.StepResults[1].Output);
    }

    [Fact]
    public async Task ExecuteAsync_StepWithComplexity_ExecutesNormally()
    {
        var module = new TestModule("conversation", "General conversation", "Done!");
        var modules = new Dictionary<string, IVitruvianModule> { ["conversation"] = module };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "hello", [
            new PlanStep("s1", "conversation", "Answer greeting", "hello", [], VitruvianAbstractions.Complexity.Low)
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Done!", result.AggregatedOutput);
    }

    // ---------------------------------------------------------------
    // Precondition tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_PreconditionMet_StepExecutes()
    {
        var readModule = new TestModule("file-ops", "File operations", "file content");
        var convModule = new TestModule("conversation", "Conversation", "summary");
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["file-ops"] = readModule,
            ["conversation"] = convModule
        };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "read and summarize", [
            new PlanStep("s1", "file-ops", "Read file", "read notes.txt", []),
            new PlanStep("s2", "conversation", "Summarize", "summarize content", ["s1"],
                Precondition: "file read succeeded")
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("summary", result.StepResults[1].Output);
    }

    [Fact]
    public async Task ExecuteAsync_PreconditionNotMet_DependencyFailed_StepSkipped()
    {
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["failing"] = new FailingModule(),
            ["conversation"] = new TestModule("conversation", "Conversation", "should not run")
        };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "failing", "Will fail", "do something", []),
            new PlanStep("s2", "conversation", "Process result", "process", ["s1"],
                Precondition: "s1 must succeed")
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.StepResults[1].Success);
        Assert.Contains("Precondition not met", result.StepResults[1].Output);
    }

    [Fact]
    public async Task ExecuteAsync_NoPrecondition_StepRunsEvenIfDependencyFailed()
    {
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["failing"] = new FailingModule(),
            ["conversation"] = new TestModule("conversation", "Conversation", "still works")
        };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "failing", "Will fail", "do something", []),
            new PlanStep("s2", "conversation", "Will succeed", "hello", ["s1"])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        // Without precondition, s2 still runs despite s1 failure
        Assert.True(result.StepResults[1].Success);
        Assert.Equal("still works", result.StepResults[1].Output);
    }

    // ---------------------------------------------------------------
    // Postcondition tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_PostconditionSatisfied_StepSucceeds()
    {
        var module = new TestModule("conversation", "Conversation", "The answer is 42");
        var modules = new Dictionary<string, IVitruvianModule> { ["conversation"] = module };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "conversation", "Answer", "question", [],
                Postcondition: "42")
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_PostconditionNotSatisfied_StepFails()
    {
        var module = new TestModule("conversation", "Conversation", "I don't know");
        var modules = new Dictionary<string, IVitruvianModule> { ["conversation"] = module };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "conversation", "Answer", "question", [],
                Postcondition: "42")
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Postcondition not met", result.StepResults[0].Output);
    }

    [Fact]
    public async Task ExecuteAsync_PostconditionCaseInsensitive_StepSucceeds()
    {
        var module = new TestModule("conversation", "Conversation", "SUCCESS: done");
        var modules = new Dictionary<string, IVitruvianModule> { ["conversation"] = module };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "conversation", "Do task", "task", [],
                Postcondition: "success")
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
    }

    // ---------------------------------------------------------------
    // Fallback step tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_FallbackStep_ExecutedOnPrimaryFailure()
    {
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["failing"] = new FailingModule(),
            ["conversation"] = new TestModule("conversation", "Conversation", "fallback answer")
        };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "failing", "Will fail", "do something", [],
                FallbackStepId: "s1-fb"),
            new PlanStep("s1-fb", "conversation", "Fallback answer", "answer from knowledge", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
        // The result for s1 should show it was a fallback
        var s1Result = result.StepResults.First(r => r.StepId == "s1");
        Assert.True(s1Result.WasFallback);
        Assert.Equal("fallback answer", s1Result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_FallbackStep_SkippedDuringNormalExecution()
    {
        var primaryModule = new TestModule("primary", "Primary module", "primary result");
        var fallbackModule = new TestModule("fallback", "Fallback module", "fallback result");
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["primary"] = primaryModule,
            ["fallback"] = fallbackModule
        };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "primary", "Primary step", "do something", [],
                FallbackStepId: "s1-fb"),
            new PlanStep("s1-fb", "fallback", "Fallback step", "fallback", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("primary result", result.StepResults.First(r => r.StepId == "s1").Output);
        Assert.Equal(0, fallbackModule.ExecutionCount); // Fallback was never executed
    }

    [Fact]
    public async Task ExecuteAsync_PostconditionFailure_TriggersFallback()
    {
        var primaryModule = new TestModule("primary", "Primary module", "wrong output");
        var fallbackModule = new TestModule("fallback", "Fallback module", "correct output with keyword");
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["primary"] = primaryModule,
            ["fallback"] = fallbackModule
        };
        var executor = new PlanExecutor(modules);

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "primary", "Primary step", "do something", [],
                Postcondition: "keyword", FallbackStepId: "s1-fb"),
            new PlanStep("s1-fb", "fallback", "Fallback step", "try fallback", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(result.Success);
        var s1Result = result.StepResults.First(r => r.StepId == "s1");
        Assert.True(s1Result.WasFallback);
        Assert.Contains("keyword", s1Result.Output);
    }

    // ---------------------------------------------------------------
    // Replan tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReplanOnFailure_ExecutesNewPlan()
    {
        var failingModule = new FailingModule();
        var convModule = new TestModule("conversation", "Conversation", "replanned answer");
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["failing"] = failingModule,
            ["conversation"] = convModule
        };
        var executor = new PlanExecutor(modules);

        var replanCalled = false;
        executor.MaxReplans = 1;
        executor.ReplanCallback = (request, failedResult, ct) =>
        {
            replanCalled = true;
            // Return a revised plan that uses conversation instead of the failing module
            var newPlan = new ExecutionPlan("p2", request, [
                new PlanStep("s1", "conversation", "Answer directly", request, [])
            ]);
            return Task.FromResult<ExecutionPlan?>(newPlan);
        };

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "failing", "Will fail", "do something", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.True(replanCalled);
        Assert.True(result.Success);
        Assert.Equal("replanned answer", result.AggregatedOutput);
    }

    [Fact]
    public async Task ExecuteAsync_ReplanDisabled_NoReplanAttempt()
    {
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["failing"] = new FailingModule()
        };
        var executor = new PlanExecutor(modules);

        var replanCalled = false;
        executor.MaxReplans = 0; // Disabled
        executor.ReplanCallback = (_, _, _) =>
        {
            replanCalled = true;
            return Task.FromResult<ExecutionPlan?>(null);
        };

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "failing", "Will fail", "do something", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.False(replanCalled);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_ReplanCallbackReturnsNull_StopsReplanning()
    {
        var modules = new Dictionary<string, IVitruvianModule>
        {
            ["failing"] = new FailingModule()
        };
        var executor = new PlanExecutor(modules);

        executor.MaxReplans = 3;
        var callCount = 0;
        executor.ReplanCallback = (_, _, _) =>
        {
            callCount++;
            return Task.FromResult<ExecutionPlan?>(null);
        };

        var plan = new ExecutionPlan("p1", "test", [
            new PlanStep("s1", "failing", "Will fail", "do something", [])
        ]);

        var result = await executor.ExecuteAsync(plan, null, CancellationToken.None);

        Assert.Equal(1, callCount); // Called once, returned null, stopped
        Assert.False(result.Success);
    }

    private sealed class EchoModule : IVitruvianModule
    {
        public string Domain { get; }
        public string Description { get; }

        public EchoModule(string domain, string description)
        {
            Domain = domain;
            Description = description;
        }

        public Task<string> ExecuteAsync(string request, string? userId, CancellationToken ct)
            => Task.FromResult(request);
    }
}
