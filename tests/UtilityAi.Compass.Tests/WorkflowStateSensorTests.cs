using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Workflow;
using UtilityAi.Compass.Runtime.Sensors;
using UtilityAi.Memory;
using UtilityAi.Utils;
using Rt = UtilityAi.Utils.Runtime;

namespace UtilityAi.Compass.Tests;

public class WorkflowStateSensorTests
{
    [Fact]
    public async Task SenseAsync_PublishesNothing_WhenNoWorkflowRunExists()
    {
        var store = new InMemoryStore();
        var sensor = new WorkflowStateSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        Assert.Null(bus.GetOrDefault<ActiveWorkflow>());
    }

    [Fact]
    public async Task SenseAsync_PublishesActiveWorkflow_WhenRunIsActive()
    {
        var store = new InMemoryStore();
        var run = new WorkflowRunRecord("run-1", "wf-1", DateTimeOffset.UtcNow);
        await store.StoreAsync(run, DateTimeOffset.UtcNow);

        var sensor = new WorkflowStateSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var active = bus.GetOrDefault<ActiveWorkflow>();
        Assert.NotNull(active);
        Assert.Equal("wf-1", active.WorkflowId);
        Assert.Equal("run-1", active.RunId);
        Assert.Equal(WorkflowStatus.Active, active.Status);
    }

    [Fact]
    public async Task SenseAsync_DoesNotPublish_WhenRunIsCompleted()
    {
        var store = new InMemoryStore();
        var run = new WorkflowRunRecord("run-1", "wf-1", DateTimeOffset.UtcNow,
            EndedUtc: DateTimeOffset.UtcNow, Outcome: WorkflowStatus.Completed);
        await store.StoreAsync(run, DateTimeOffset.UtcNow);

        var sensor = new WorkflowStateSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        Assert.Null(bus.GetOrDefault<ActiveWorkflow>());
    }

    [Fact]
    public async Task SenseAsync_DoesNotPublish_WhenRunIsAborted()
    {
        var store = new InMemoryStore();
        var run = new WorkflowRunRecord("run-1", "wf-1", DateTimeOffset.UtcNow,
            Outcome: WorkflowStatus.Aborted);
        await store.StoreAsync(run, DateTimeOffset.UtcNow);

        var sensor = new WorkflowStateSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        Assert.Null(bus.GetOrDefault<ActiveWorkflow>());
    }

    [Fact]
    public async Task SenseAsync_DerivesAwaitingUser_WhenStepNeedsInput()
    {
        var store = new InMemoryStore();
        var run = new WorkflowRunRecord("run-1", "wf-1", DateTimeOffset.UtcNow);
        await store.StoreAsync(run, DateTimeOffset.UtcNow);

        var step = new WorkflowStepRecord("run-1", "step-2", 1, DateTimeOffset.UtcNow,
            Outcome: StepOutcome.NeedsUserInput);
        await store.StoreAsync(step, DateTimeOffset.UtcNow);

        var sensor = new WorkflowStateSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var active = bus.GetOrDefault<ActiveWorkflow>();
        Assert.NotNull(active);
        Assert.Equal(WorkflowStatus.AwaitingUser, active.Status);
        Assert.Equal("step-2", active.CurrentStepId);
    }

    [Fact]
    public async Task SenseAsync_DerivesValidating_WhenStepNeedsValidation()
    {
        var store = new InMemoryStore();
        var run = new WorkflowRunRecord("run-1", "wf-1", DateTimeOffset.UtcNow);
        await store.StoreAsync(run, DateTimeOffset.UtcNow);

        var step = new WorkflowStepRecord("run-1", "step-1", 1, DateTimeOffset.UtcNow,
            Outcome: StepOutcome.NeedsValidation);
        await store.StoreAsync(step, DateTimeOffset.UtcNow);

        var sensor = new WorkflowStateSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var active = bus.GetOrDefault<ActiveWorkflow>();
        Assert.NotNull(active);
        Assert.Equal(WorkflowStatus.Validating, active.Status);
    }

    [Fact]
    public async Task SenseAsync_DerivesRepairing_WhenStepFailedRetryable()
    {
        var store = new InMemoryStore();
        var run = new WorkflowRunRecord("run-1", "wf-1", DateTimeOffset.UtcNow);
        await store.StoreAsync(run, DateTimeOffset.UtcNow);

        var step = new WorkflowStepRecord("run-1", "step-1", 1, DateTimeOffset.UtcNow,
            Outcome: StepOutcome.FailedRetryable);
        await store.StoreAsync(step, DateTimeOffset.UtcNow);

        var sensor = new WorkflowStateSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var active = bus.GetOrDefault<ActiveWorkflow>();
        Assert.NotNull(active);
        Assert.Equal(WorkflowStatus.Repairing, active.Status);
    }
}
