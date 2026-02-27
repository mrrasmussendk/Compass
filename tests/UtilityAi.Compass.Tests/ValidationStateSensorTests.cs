using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Runtime.Sensors;
using UtilityAi.Memory;
using UtilityAi.Utils;
using Rt = UtilityAi.Utils.Runtime;

namespace UtilityAi.Compass.Tests;

public class ValidationStateSensorTests
{
    [Fact]
    public async Task SenseAsync_PublishesNothing_WhenNoActiveWorkflow()
    {
        var store = new InMemoryStore();
        var sensor = new ValidationStateSensor(store);
        var bus = new EventBus();
        var rt = new Rt(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        Assert.Null(bus.GetOrDefault<NeedsValidation>());
        Assert.Null(bus.GetOrDefault<ValidationOutcome>());
    }

    [Fact]
    public async Task SenseAsync_PublishesNeedsValidation_WhenPendingRequestExists()
    {
        var store = new InMemoryStore();
        var sensor = new ValidationStateSensor(store);
        var bus = new EventBus();
        bus.Publish(new ActiveWorkflow("wf-1", "run-1", "step-1", WorkflowStatus.Validating));
        var rt = new Rt(bus, 0);

        var request = new NeedsValidation("wf-1", "run-1", ValidationScope.Step, "step-1");
        await store.StoreAsync(request, DateTimeOffset.UtcNow);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var published = bus.GetOrDefault<NeedsValidation>();
        Assert.NotNull(published);
        Assert.Equal("step-1", published.TargetId);
    }

    [Fact]
    public async Task SenseAsync_PublishesValidationOutcome_WhenRecordExists()
    {
        var store = new InMemoryStore();
        var sensor = new ValidationStateSensor(store);
        var bus = new EventBus();
        bus.Publish(new ActiveWorkflow("wf-1", "run-1", "step-1", WorkflowStatus.Validating));
        var rt = new Rt(bus, 0);

        var request = new NeedsValidation("wf-1", "run-1", ValidationScope.Step, "step-1");
        await store.StoreAsync(request, DateTimeOffset.UtcNow);

        var record = new ValidationRecord("run-1", "step-1", ValidationOutcomeTag.Pass, "All good");
        await store.StoreAsync(record, DateTimeOffset.UtcNow);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var outcome = bus.GetOrDefault<ValidationOutcome>();
        Assert.NotNull(outcome);
        Assert.Equal(ValidationOutcomeTag.Pass, outcome.Outcome);
        Assert.Equal("All good", outcome.Diagnostics);
    }

    [Fact]
    public async Task SenseAsync_DoesNotPublishOutcome_WhenNoValidationRecordExists()
    {
        var store = new InMemoryStore();
        var sensor = new ValidationStateSensor(store);
        var bus = new EventBus();
        bus.Publish(new ActiveWorkflow("wf-1", "run-1", "step-1", WorkflowStatus.Validating));
        var rt = new Rt(bus, 0);

        var request = new NeedsValidation("wf-1", "run-1", ValidationScope.Step, "step-1");
        await store.StoreAsync(request, DateTimeOffset.UtcNow);

        await sensor.SenseAsync(rt, CancellationToken.None);

        Assert.Null(bus.GetOrDefault<ValidationOutcome>());
    }
}
