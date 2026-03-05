using VitruvianAbstractions.Interfaces;
using Xunit;

namespace VitruvianTests;

public sealed class ModelClientStructuredOutputExtensionsTests
{
    private sealed class CapturingModelClient(string responseText) : IModelClient
    {
        public ModelRequest? LastRequest { get; private set; }

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(responseText);

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ModelResponse { Text = responseText });
        }

        public Task<string> CompleteAsync(
            string systemMessage,
            string userMessage,
            IReadOnlyList<ModelTool>? tools = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(responseText);
    }

    private sealed class ContactInfo
    {
        public required string Name { get; init; }
        public required string Email { get; init; }
        public bool DemoRequested { get; init; }
    }

    [Fact]
    public async Task GenerateStructuredAsync_DeserializesTypedOutput_AndInjectsSchemaInstructions()
    {
        var client = new CapturingModelClient("""{"name":"John Smith","email":"john@example.com","demoRequested":true}""");

        var result = await client.GenerateStructuredAsync<ContactInfo>("Extract contact details");

        Assert.Equal("John Smith", result.Name);
        Assert.Equal("john@example.com", result.Email);
        Assert.True(result.DemoRequested);
        Assert.NotNull(client.LastRequest);
        Assert.Equal("Extract contact details", client.LastRequest!.Prompt);
        Assert.Contains("JSON Schema", client.LastRequest.SystemMessage);
        Assert.Contains("\"Name\"", client.LastRequest.SystemMessage);
        Assert.Contains("\"Email\"", client.LastRequest.SystemMessage);
    }

    [Fact]
    public async Task GenerateStructuredAsync_MarkdownWrappedJson_ParsesSuccessfully()
    {
        var client = new CapturingModelClient(
            """
            ```json
            {"name":"Jane Doe","email":"jane@example.com","demoRequested":false}
            ```
            """);

        var result = await client.GenerateStructuredAsync<ContactInfo>("Extract contact details");

        Assert.Equal("Jane Doe", result.Name);
        Assert.Equal("jane@example.com", result.Email);
        Assert.False(result.DemoRequested);
    }

    [Fact]
    public async Task GenerateStructuredAsync_InvalidJson_ThrowsInvalidOperationException()
    {
        var client = new CapturingModelClient("not json");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateStructuredAsync<ContactInfo>("Extract contact details"));

        Assert.Contains("Failed to parse structured output", ex.Message);
    }
}
