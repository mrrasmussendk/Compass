using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.WeatherModule;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public sealed class WeatherWebModuleTests
{
    [Fact]
    public void Propose_ReturnsProposal_ForWeatherRequest()
    {
        var module = new WeatherWebModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("what is the weather in Aarhus?"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("weather-web.current", proposals[0].Id);
    }

    [Fact]
    public void Propose_ReturnsNoProposal_ForNonWeatherRequest()
    {
        var module = new WeatherWebModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("summarize this paragraph"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }
}
