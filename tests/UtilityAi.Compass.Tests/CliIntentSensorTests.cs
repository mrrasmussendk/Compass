using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Runtime.Sensors;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class CliIntentSensorTests
{
    [Theory]
    [InlineData("read config", CliVerb.Read)]
    [InlineData("get users", CliVerb.Read)]
    [InlineData("show settings", CliVerb.Read)]
    [InlineData("list items", CliVerb.Read)]
    [InlineData("view logs", CliVerb.Read)]
    [InlineData("fetch data", CliVerb.Read)]
    [InlineData("display results", CliVerb.Read)]
    public async Task SenseAsync_DetectsReadVerb(string text, CliVerb expectedVerb)
    {
        var sensor = new CliIntentSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest(text));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var intent = bus.GetOrDefault<CliIntent>();
        Assert.NotNull(intent);
        Assert.Equal(expectedVerb, intent.Verb);
        Assert.True(intent.Confidence >= 0.8);
    }

    [Theory]
    [InlineData("write config", CliVerb.Write)]
    [InlineData("create user", CliVerb.Write)]
    [InlineData("add item", CliVerb.Write)]
    [InlineData("set value", CliVerb.Write)]
    [InlineData("store record", CliVerb.Write)]
    [InlineData("save document", CliVerb.Write)]
    public async Task SenseAsync_DetectsWriteVerb(string text, CliVerb expectedVerb)
    {
        var sensor = new CliIntentSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest(text));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var intent = bus.GetOrDefault<CliIntent>();
        Assert.NotNull(intent);
        Assert.Equal(expectedVerb, intent.Verb);
    }

    [Theory]
    [InlineData("update config", CliVerb.Update)]
    [InlineData("edit settings", CliVerb.Update)]
    [InlineData("modify user", CliVerb.Update)]
    [InlineData("change value", CliVerb.Update)]
    [InlineData("patch record", CliVerb.Update)]
    public async Task SenseAsync_DetectsUpdateVerb(string text, CliVerb expectedVerb)
    {
        var sensor = new CliIntentSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest(text));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var intent = bus.GetOrDefault<CliIntent>();
        Assert.NotNull(intent);
        Assert.Equal(expectedVerb, intent.Verb);
    }

    [Fact]
    public async Task SenseAsync_DoesNotPublish_WhenNoVerbMatches()
    {
        var sensor = new CliIntentSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest("hello world"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var intent = bus.GetOrDefault<CliIntent>();
        Assert.Null(intent);
    }

    [Fact]
    public async Task SenseAsync_DoesNotOverwrite_WhenIntentAlreadyPresent()
    {
        var sensor = new CliIntentSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest("read config"));
        bus.Publish(new CliIntent(CliVerb.Write, "override", 1.0));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var intent = bus.GetOrDefault<CliIntent>();
        Assert.NotNull(intent);
        Assert.Equal(CliVerb.Write, intent.Verb);
        Assert.Equal("override", intent.Target);
    }

    [Fact]
    public async Task SenseAsync_DoesNotPublish_WhenNoUserRequest()
    {
        var sensor = new CliIntentSensor();
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var intent = bus.GetOrDefault<CliIntent>();
        Assert.Null(intent);
    }

    [Theory]
    [InlineData("get users", "users")]
    [InlineData("read config value", "config")]
    [InlineData("show settings", "settings")]
    public async Task SenseAsync_ExtractsTarget(string text, string expectedTarget)
    {
        var sensor = new CliIntentSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest(text));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var intent = bus.GetOrDefault<CliIntent>();
        Assert.NotNull(intent);
        Assert.Equal(expectedTarget, intent.Target);
    }

    [Fact]
    public async Task SenseAsync_TargetIsNull_WhenNoWordFollowsKeyword()
    {
        var sensor = new CliIntentSensor();
        var bus = new EventBus();
        bus.Publish(new UserRequest("read"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        await sensor.SenseAsync(rt, CancellationToken.None);

        var intent = bus.GetOrDefault<CliIntent>();
        Assert.NotNull(intent);
        Assert.Null(intent.Target);
    }
}
