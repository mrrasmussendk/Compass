using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.WeatherModule;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class WeatherWorkflowTests
{
    [Fact]
    public void Define_ReturnsCorrectWorkflowDefinition()
    {
        var wf = new WeatherWorkflow();
        var def = wf.Define();

        Assert.Equal("weather-web", def.WorkflowId);
        Assert.Equal("Weather Lookup", def.DisplayName);
        Assert.Contains(GoalTag.Answer, def.Goals);
        Assert.Contains(Lane.Execute, def.Lanes);
        Assert.Single(def.Steps);
        Assert.Equal("current", def.Steps[0].StepId);
        Assert.True(def.CanInterrupt);
        Assert.Equal(0.3, def.EstimatedCost);
        Assert.Equal(0.0, def.RiskLevel);
    }

    [Fact]
    public void ProposeStart_ReturnsProposal_ForWeatherRequest()
    {
        var wf = new WeatherWorkflow();
        var bus = new EventBus();
        bus.Publish(new UserRequest("what is the weather in Aarhus?"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = wf.ProposeStart(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("weather-web.current", proposals[0].Id);
    }

    [Fact]
    public void ProposeStart_ReturnsEmpty_ForNonWeatherRequest()
    {
        var wf = new WeatherWorkflow();
        var bus = new EventBus();
        bus.Publish(new UserRequest("summarize this paragraph"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        Assert.Empty(wf.ProposeStart(rt));
    }

    [Fact]
    public void ProposeStart_ReturnsEmpty_WhenNoUserRequest()
    {
        var wf = new WeatherWorkflow();
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        Assert.Empty(wf.ProposeStart(rt));
    }

    [Fact]
    public void ProposeSteps_ReturnsEmpty()
    {
        var wf = new WeatherWorkflow();
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);
        var active = new ActiveWorkflow("weather-web", "run-1", "current", WorkflowStatus.Active);

        Assert.Empty(wf.ProposeSteps(rt, active));
    }
}
