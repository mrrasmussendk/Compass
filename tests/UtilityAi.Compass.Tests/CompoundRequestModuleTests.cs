using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.StandardModules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class CompoundRequestModuleTests
{
    private sealed class StubModelClient : IModelClient
    {
        private readonly Func<ModelRequest, string> _handler;

        public StubModelClient(Func<ModelRequest, string> handler)
        {
            _handler = handler;
        }

        public StubModelClient(string fixedResponse)
            : this(_ => fixedResponse) { }

        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult($"stub:{prompt}");

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ModelResponse { Text = _handler(request) });
    }

    [Fact]
    public void Propose_ReturnsEmpty_WhenNoMultiStepRequest()
    {
        var module = new CompoundRequestModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file test.txt with content hello"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public void Propose_ReturnsEmpty_WhenMultiStepIsNotCompound()
    {
        var module = new CompoundRequestModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file test.txt with content hello"));
        bus.Publish(new MultiStepRequest("create file test.txt with content hello", 1, IsCompound: false));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public void Propose_ReturnsGuidance_WhenNoModelClient()
    {
        var module = new CompoundRequestModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file u.txt then tell me about rainbows"));
        bus.Publish(new MultiStepRequest("create file u.txt then tell me about rainbows", 2, IsCompound: true));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("compound-request.respond", proposals[0].Id);
    }

    [Fact]
    public async Task Propose_ReturnsGuidanceResponse_WhenNoModelClient()
    {
        var module = new CompoundRequestModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file u.txt then tell me about rainbows"));
        bus.Publish(new MultiStepRequest("create file u.txt then tell me about rainbows", 2, IsCompound: true));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        await proposals[0].Act(CancellationToken.None);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        Assert.Contains("multiple tasks", response.Text);
    }

    [Fact]
    public void Propose_ReturnsHandleProposal_WhenModelClientAvailable()
    {
        var modelClient = new StubModelClient("[]");
        var module = new CompoundRequestModule(modelClient);
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file u.txt then tell me about rainbows"));
        bus.Publish(new MultiStepRequest("create file u.txt then tell me about rainbows", 2, IsCompound: true));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("compound-request.handle", proposals[0].Id);
    }

    [Fact]
    public async Task Handle_CreatesFileAndResponds_WhenLlmExtractsFileOps()
    {
        var fileName = $"test-compound-{Guid.NewGuid()}.txt";

        try
        {
            var callCount = 0;
            var modelClient = new StubModelClient(request =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call: extract file operations (relative path only)
                    return $"[{{\"filename\": \"{fileName}\", \"content\": \"gold\"}}]";
                }
                // Second call: conversational response
                return "The colors of the rainbow are red, orange, yellow, green, blue, indigo, and violet.";
            });

            var module = new CompoundRequestModule(modelClient);
            var bus = new EventBus();
            bus.Publish(new UserRequest("Make a file called u.txt insert the word gold. Then give me the colors of the rainbow"));
            bus.Publish(new MultiStepRequest("Make a file called u.txt insert the word gold. Then give me the colors of the rainbow", 2, IsCompound: true));
            var rt = new UtilityAi.Utils.Runtime(bus, 0);

            var proposals = module.Propose(rt).ToList();
            await proposals[0].Act(CancellationToken.None);

            // Verify file was created
            Assert.True(File.Exists(fileName));
            Assert.Equal("gold", File.ReadAllText(fileName));

            // Verify response contains both file creation confirmation and answer
            var response = bus.GetOrDefault<AiResponse>();
            Assert.NotNull(response);
            Assert.Contains(fileName, response.Text);
            Assert.Contains("rainbow", response.Text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(fileName))
                File.Delete(fileName);
        }
    }

    [Fact]
    public async Task Handle_AnswersQuestion_WhenNoFileOps()
    {
        var modelClient = new StubModelClient(request =>
        {
            if (request.SystemMessage?.Contains("Extract file operations") == true)
                return "[]"; // No file operations
            return "Here is the answer to your question.";
        });

        var module = new CompoundRequestModule(modelClient);
        var bus = new EventBus();
        bus.Publish(new UserRequest("Tell me about cats and then about dogs"));
        bus.Publish(new MultiStepRequest("Tell me about cats and then about dogs", 2, IsCompound: true));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        await proposals[0].Act(CancellationToken.None);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        Assert.Equal("Here is the answer to your question.", response.Text);
    }

    [Fact]
    public async Task Handle_FallsBackGracefully_WhenLlmReturnsInvalidJson()
    {
        var modelClient = new StubModelClient(request =>
        {
            if (request.SystemMessage?.Contains("Extract file operations") == true)
                return "Sorry, I cannot parse that."; // Invalid JSON
            return "I'll do my best to help.";
        });

        var module = new CompoundRequestModule(modelClient);
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file x.txt then tell me a joke"));
        bus.Publish(new MultiStepRequest("create file x.txt then tell me a joke", 2, IsCompound: true));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        await proposals[0].Act(CancellationToken.None);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        // Should still produce a response even if file extraction fails
        Assert.False(string.IsNullOrEmpty(response.Text));
    }

    [Fact]
    public void Propose_HasPositiveUtility()
    {
        var module = new CompoundRequestModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("create file u.txt then tell me about rainbows"));
        bus.Publish(new MultiStepRequest("create file u.txt then tell me about rainbows", 2, IsCompound: true));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);
        Assert.True(proposals[0].Utility(rt) > 0);
    }

    [Theory]
    [InlineData("/etc/passwd")]
    [InlineData("../../../sensitive.txt")]
    [InlineData("sub/../../../escape.txt")]
    public void ExecuteFileOperations_RejectsPathTraversal(string maliciousPath)
    {
        var results = CompoundRequestModule.ExecuteFileOperations([(maliciousPath, "evil")]);

        Assert.Single(results);
        Assert.Contains("Skipped", results[0]);
        Assert.Contains("only relative paths", results[0]);
    }
}
