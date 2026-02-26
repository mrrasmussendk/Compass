using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using UtilityAi.Capabilities;
using UtilityAi.Consideration;
using UtilityAi.Consideration.General;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Utils;

namespace Compass.SamplePlugin.OpenAi;

[CompassCapability("openai", priority: 2)]
[CompassGoals(GoalTag.Answer)]
[CompassLane(Lane.Communicate)]
[CompassCost(0.5)]
public sealed class OpenAiModule : ICapabilityModule
{
    private static readonly HttpClient HttpClient = new();

    public IEnumerable<Proposal> Propose(Runtime rt)
    {
        var request = rt.Bus.GetOrDefault<UserRequest>();
        if (request is null) yield break;

        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey)) yield break;

        var model = Environment.GetEnvironmentVariable("COMPASS_MODEL_NAME") ?? "gpt-4o-mini";

        yield return new Proposal(
            id: "openai.chat",
            cons: [new ConstantValue(0.9)],
            act: async ct =>
            {
                var response = await CallOpenAiAsync(apiKey, model, request.Text, ct);
                rt.Bus.Publish(new AiResponse(response));
            }
        ) { Description = "Reply using OpenAI chat completion" };
    }

    private static async Task<string> CallOpenAiAsync(string apiKey, string model, string prompt, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } }
        }), Encoding.UTF8, "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(payload);
        return json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
            ?? "OpenAI returned an empty response.";
    }
}
