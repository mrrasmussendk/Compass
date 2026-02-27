using System.Text.Json;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Abstractions.Workflow;

namespace UtilityAi.Compass.WeatherModule;

/// <summary>
/// Workflow module that fetches current weather data from wttr.in.
/// Single-step workflow: detect weather request, fetch data, publish result.
/// </summary>
public sealed class WeatherWorkflow : IWorkflowModule
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    /// <inheritdoc />
    public WorkflowDefinition Define() => new(
        WorkflowId: "weather-web",
        DisplayName: "Weather Lookup",
        Goals: [GoalTag.Answer],
        Lanes: [Lane.Execute],
        Steps:
        [
            new StepDefinition(
                StepId: "current",
                DisplayName: "Fetch current weather",
                RequiresFacts: ["UserRequest"],
                ProducesFacts: ["AiResponse"],
                Idempotent: true,
                MaxRetries: 2,
                Timeout: TimeSpan.FromSeconds(10))
        ],
        CanInterrupt: true,
        EstimatedCost: 0.3,
        RiskLevel: 0.0);

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeStart(UtilityAi.Utils.Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null || !LooksLikeWeatherRequest(request.Text))
            yield break;

        var location = TryExtractLocation(request.Text) ?? "Copenhagen";
        yield return new Proposal(
            id: "weather-web.current",
            cons: [new ConstantValue(0.85)],
            act: async ct =>
            {
                var weatherText = await GetCurrentWeatherTextAsync(location, ct);
                rt.Bus.Publish(new AiResponse(weatherText));
                rt.Bus.Publish(new StepResult(StepOutcome.Succeeded, "Weather data fetched"));
            })
        { Description = "Fetch weather information from the web" };
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeSteps(UtilityAi.Utils.Runtime rt, ActiveWorkflow active) =>
        [];

    /// <inheritdoc />
    public IEnumerable<Proposal> ProposeRepair(UtilityAi.Utils.Runtime rt, ActiveWorkflow active, RepairDirective directive) =>
        [];

    private static bool LooksLikeWeatherRequest(string text) =>
        text.Contains("weather", StringComparison.OrdinalIgnoreCase)
        || text.Contains("temperature", StringComparison.OrdinalIgnoreCase)
        || text.Contains("forecast", StringComparison.OrdinalIgnoreCase);

    private static string? TryExtractLocation(string text)
    {
        const string marker = " in ";
        var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return null;

        var value = text[(index + marker.Length)..].Trim().TrimEnd('.', '?', '!');
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static async Task<string> GetCurrentWeatherTextAsync(string location, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"https://wttr.in/{Uri.EscapeDataString(location)}?format=j1";
            using var response = await HttpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return $"I couldn't fetch weather for {location} right now (HTTP {(int)response.StatusCode}).";

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);

            if (!json.RootElement.TryGetProperty("current_condition", out var currentConditions)
                || currentConditions.ValueKind != JsonValueKind.Array
                || currentConditions.GetArrayLength() == 0)
            {
                return $"I couldn't parse weather data for {location}.";
            }

            var current = currentConditions[0];
            var temperatureC = current.GetProperty("temp_C").GetString();
            var windKmph = current.GetProperty("windspeedKmph").GetString();
            var description = current.GetProperty("weatherDesc")[0].GetProperty("value").GetString();

            if (string.IsNullOrWhiteSpace(temperatureC) || string.IsNullOrWhiteSpace(description))
                return $"I couldn't parse weather data for {location}.";

            return $"Current weather in {location}: {temperatureC}°C, {description}, wind {windKmph ?? "?"} km/h.";
        }
        catch (HttpRequestException)
        {
            return $"I couldn't fetch weather for {location} right now.";
        }
        catch (TaskCanceledException)
        {
            return $"Weather lookup for {location} timed out.";
        }
        catch (JsonException)
        {
            return $"I couldn't parse weather data for {location}.";
        }
        catch (Exception ex) when (ex is KeyNotFoundException or IndexOutOfRangeException)
        {
            return $"I couldn't parse weather data for {location}.";
        }
    }
}
