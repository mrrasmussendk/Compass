using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Runtime;

namespace UtilityAi.Compass.Tests;

/// <summary>
/// Comprehensive tests for <see cref="CompoundRequestOrchestrator"/>, covering:
/// - Compound detection heuristics (indicators, multiple verbs, edge cases)
/// - LLM-based decomposition (valid JSON, invalid JSON, empty, cancellation)
/// - Race conditions and concurrency (parallel decomposition, cancellation mid-flight)
/// </summary>
public class CompoundRequestOrchestratorTests
{
    /// <summary>Test double that returns a fixed response for any model call.</summary>
    private sealed class StubModelClient(string response) : IModelClient
    {
        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(response);

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ModelResponse { Text = response });
    }

    /// <summary>Test double that uses a delegate, useful for varying responses per call.</summary>
    private sealed class DelegatingModelClient(Func<ModelRequest, CancellationToken, Task<ModelResponse>> handler) : IModelClient
    {
        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult("stub");

        public Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken cancellationToken = default)
            => handler(request, cancellationToken);
    }

    // ────────────────────────────────────────────────────────────
    // IsCompoundRequest — Detection Heuristics
    // ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("create file u.txt then tell me about rainbows")]
    [InlineData("write hello to file.txt and then send an SMS")]
    [InlineData("delete the old log afterwards create a new one")]
    [InlineData("summarize the report after that email it to me")]
    [InlineData("read the file next update the database")]
    [InlineData("generate a report followed by sending it via email")]
    [InlineData("create a backup after check disk space")]
    public void IsCompoundRequest_DetectsSequentialIndicators(string text)
    {
        Assert.True(CompoundRequestOrchestrator.IsCompoundRequest(text));
    }

    [Theory]
    [InlineData("create a file and write some content")]
    [InlineData("read the file and delete the backup")]
    [InlineData("insert data and update the index")]
    public void IsCompoundRequest_DetectsMultipleActionVerbs(string text)
    {
        Assert.True(CompoundRequestOrchestrator.IsCompoundRequest(text));
    }

    [Theory]
    [InlineData("create file test.txt with content hello")]
    [InlineData("tell me about the weather")]
    [InlineData("what is the capital of France")]
    [InlineData("summarize this document")]
    [InlineData("hello")]
    [InlineData("I saw a bird and then i shot it what should i do?")]
    public void IsCompoundRequest_ReturnsFalse_ForSimpleRequests(string text)
    {
        Assert.False(CompoundRequestOrchestrator.IsCompoundRequest(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsCompoundRequest_ReturnsFalse_ForEmptyOrNullInput(string? text)
    {
        Assert.False(CompoundRequestOrchestrator.IsCompoundRequest(text));
    }

    [Fact]
    public void IsCompoundRequest_IsCaseInsensitive()
    {
        Assert.True(CompoundRequestOrchestrator.IsCompoundRequest("CREATE file THEN DELETE backup"));
        Assert.True(CompoundRequestOrchestrator.IsCompoundRequest("Write data AND THEN read it back"));
    }

    [Fact]
    public void IsCompoundRequest_HandlesIndicatorsAtBoundaries()
    {
        // " then " needs spaces, so "then" alone shouldn't match
        Assert.False(CompoundRequestOrchestrator.IsCompoundRequest("do something thenext thing"));
        // But with proper spacing it should
        Assert.True(CompoundRequestOrchestrator.IsCompoundRequest("do something then do next thing"));
    }

    [Fact]
    public void IsCompoundRequest_ExactlyTwoVerbs_IsCompound()
    {
        Assert.True(CompoundRequestOrchestrator.IsCompoundRequest("create and write"));
    }

    [Fact]
    public void IsCompoundRequest_SingleVerb_IsNotCompound()
    {
        Assert.False(CompoundRequestOrchestrator.IsCompoundRequest("create a file"));
    }

    // ────────────────────────────────────────────────────────────
    // DecomposeRequestAsync — LLM Decomposition
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DecomposeRequestAsync_ReturnsOriginal_WhenNoModelClient()
    {
        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            null, "create file then tell me a joke", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("create file then tell me a joke", result[0]);
    }

    [Fact]
    public async Task DecomposeRequestAsync_SplitsIntoSubtasks_WhenLlmReturnsValidJson()
    {
        var modelClient = new StubModelClient(
            "[\"Create a file called u.txt with the word gold\", \"Tell me the colors of the rainbow\"]");

        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "create file u.txt with gold then give me rainbow colors", CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Create a file called u.txt with the word gold", result[0]);
        Assert.Equal("Tell me the colors of the rainbow", result[1]);
    }

    [Fact]
    public async Task DecomposeRequestAsync_ReturnsOriginal_WhenLlmReturnsInvalidJson()
    {
        var modelClient = new StubModelClient("Sorry, I can't parse that.");

        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "create file then tell me a joke", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("create file then tell me a joke", result[0]);
    }

    [Fact]
    public async Task DecomposeRequestAsync_ReturnsOriginal_WhenLlmReturnsEmptyArray()
    {
        var modelClient = new StubModelClient("[]");

        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "create file then tell me a joke", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("create file then tell me a joke", result[0]);
    }

    [Fact]
    public async Task DecomposeRequestAsync_ReturnsOriginal_WhenLlmReturnsJsonObject()
    {
        var modelClient = new StubModelClient("{\"error\": \"unexpected\"}");

        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "create file then tell me a joke", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("create file then tell me a joke", result[0]);
    }

    [Fact]
    public async Task DecomposeRequestAsync_SkipsWhitespaceOnlyElements()
    {
        var modelClient = new StubModelClient("[\"task one\", \"  \", \"task two\", \"\"]");

        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "compound input", CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("task one", result[0]);
        Assert.Equal("task two", result[1]);
    }

    [Fact]
    public async Task DecomposeRequestAsync_SkipsNullElements()
    {
        var modelClient = new StubModelClient("[\"task one\", null, \"task two\"]");

        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "compound input", CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("task one", result[0]);
        Assert.Equal("task two", result[1]);
    }

    [Fact]
    public async Task DecomposeRequestAsync_ReturnsSingleElement_WhenLlmReturnsSingleTaskArray()
    {
        var modelClient = new StubModelClient("[\"just one task\"]");

        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "just one task", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("just one task", result[0]);
    }

    [Fact]
    public async Task DecomposeRequestAsync_HandlesThreeOrMoreSubtasks()
    {
        var modelClient = new StubModelClient(
            "[\"create file.txt\", \"send an SMS\", \"check the weather\"]");

        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "create file.txt then send an SMS then check the weather",
            CancellationToken.None);

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task DecomposeRequestAsync_ReturnsOriginal_WhenLlmReturnsArrayWithOnlyWhitespace()
    {
        var modelClient = new StubModelClient("[\" \", \"  \"]");

        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "compound input", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("compound input", result[0]);
    }

    // ────────────────────────────────────────────────────────────
    // Cancellation Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DecomposeRequestAsync_ThrowsOperationCanceled_WhenAlreadyCancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var modelClient = new DelegatingModelClient((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new ModelResponse { Text = "[]" });
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CompoundRequestOrchestrator.DecomposeRequestAsync(modelClient, "input", cts.Token));
    }

    [Fact]
    public async Task DecomposeRequestAsync_ThrowsOperationCanceled_WhenCancelledMidFlight()
    {
        var cts = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var modelClient = new DelegatingModelClient(async (_, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return new ModelResponse { Text = "[\"a\", \"b\"]" };
        });

        var decomposeTask = CompoundRequestOrchestrator.DecomposeRequestAsync(modelClient, "input", cts.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            decomposeTask);
    }

    // ────────────────────────────────────────────────────────────
    // Race Conditions & Concurrency Tests
    // ────────────────────────────────────────────────────────────

    [Fact]
    public async Task DecomposeRequestAsync_IsThreadSafe_WhenCalledConcurrently()
    {
        // Verify that multiple concurrent decomposition calls don't interfere with each other
        var callCount = 0;
        var modelClient = new DelegatingModelClient(async (request, ct) =>
        {
            var index = Interlocked.Increment(ref callCount);
            // Vary the response delay to increase scheduling variability
            await Task.Delay(Random.Shared.Next(1, 20), ct);
            return new ModelResponse { Text = $"[\"task-{index}-a\", \"task-{index}-b\"]" };
        });

        var tasks = Enumerable.Range(0, 20).Select(i =>
            CompoundRequestOrchestrator.DecomposeRequestAsync(
                modelClient, $"compound request {i}", CancellationToken.None));

        var results = await Task.WhenAll(tasks);

        // All 20 calls should have produced valid results
        Assert.Equal(20, results.Length);
        foreach (var result in results)
        {
            Assert.Equal(2, result.Count);
            Assert.All(result, s => Assert.False(string.IsNullOrWhiteSpace(s)));
        }

        Assert.Equal(20, callCount);
    }

    [Fact]
    public async Task DecomposeRequestAsync_ConcurrentCancellation_DoesNotCorruptOtherCalls()
    {
        // Multiple concurrent calls where some are cancelled and others succeed.
        // Verify that cancelled calls don't affect the results of successful calls.
        var barrier = new TaskCompletionSource();
        var modelClient = new DelegatingModelClient(async (request, ct) =>
        {
            // Wait for all calls to be in-flight
            await barrier.Task.WaitAsync(ct);
            return new ModelResponse { Text = "[\"sub-a\", \"sub-b\"]" };
        });

        var cancellable = new CancellationTokenSource();
        var successTasks = Enumerable.Range(0, 5).Select(_ =>
            CompoundRequestOrchestrator.DecomposeRequestAsync(
                modelClient, "compound", CancellationToken.None));
        var cancelTasks = Enumerable.Range(0, 5).Select(_ =>
            CompoundRequestOrchestrator.DecomposeRequestAsync(
                modelClient, "compound", cancellable.Token));

        var allTasks = successTasks.Concat(cancelTasks).ToArray();

        // Cancel half the tasks, then release the barrier
        cancellable.Cancel();
        barrier.SetResult();

        var outcomes = await Task.WhenAll(allTasks.Select(async t =>
        {
            try
            {
                return (Result: await t, Error: (Exception?)null);
            }
            catch (OperationCanceledException ex)
            {
                return (Result: (List<string>?)null, Error: (Exception?)ex);
            }
        }));

        // At least the 5 non-cancelled tasks should succeed
        var succeeded = outcomes.Where(o => o.Result is not null).ToList();
        Assert.True(succeeded.Count >= 5, $"Expected at least 5 successful results, got {succeeded.Count}");

        // Each successful result should have exactly 2 sub-tasks
        foreach (var outcome in succeeded)
        {
            Assert.Equal(2, outcome.Result!.Count);
        }
    }

    [Fact]
    public async Task IsCompoundRequest_ConcurrentReads_AreConsistent()
    {
        // Verify IsCompoundRequest is safe to call concurrently
        // (it is a pure static method, but this validates no hidden state mutation)
        var inputs = new[]
        {
            ("create file then delete it", true),
            ("hello world", false),
            ("write data and then read it back", true),
            ("summarize this", false),
            ("create and write and delete", true)
        };

        var tasks = Enumerable.Range(0, 100).Select(i =>
        {
            var (text, expected) = inputs[i % inputs.Length];
            return Task.Run(() =>
            {
                var result = CompoundRequestOrchestrator.IsCompoundRequest(text);
                Assert.Equal(expected, result);
            });
        });

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task DecomposeRequestAsync_SlowModel_DoesNotBlockOtherDecompositions()
    {
        // One call is slow, others are fast. Verify the fast ones complete before the slow one.
        var completionOrder = new List<int>();
        var orderLock = new object();

        var modelClient = new DelegatingModelClient(async (request, ct) =>
        {
            var isSlow = request.Prompt.Contains("slow");
            if (isSlow)
                await Task.Delay(200, ct);
            else
                await Task.Delay(10, ct);

            lock (orderLock)
            {
                completionOrder.Add(isSlow ? 0 : 1);
            }

            return new ModelResponse { Text = "[\"a\", \"b\"]" };
        });

        var slowTask = CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "slow compound", CancellationToken.None);
        var fastTasks = Enumerable.Range(0, 3).Select(_ =>
            CompoundRequestOrchestrator.DecomposeRequestAsync(
                modelClient, "fast compound", CancellationToken.None));

        await Task.WhenAll(fastTasks.Append(slowTask));

        // Fast tasks (1) should have completed before the slow one (0) in the completion order
        var slowIndex = completionOrder.IndexOf(0);
        var lastFastIndex = completionOrder.LastIndexOf(1);
        Assert.True(lastFastIndex < slowIndex,
            "At least some fast decompositions should complete before the slow one");
    }

    [Fact]
    public async Task DecomposeRequestAsync_ModelThrowsException_ReturnsOriginal()
    {
        var modelClient = new DelegatingModelClient((_, _) =>
            throw new InvalidOperationException("Model unavailable"));

        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "create file then tell joke", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("create file then tell joke", result[0]);
    }

    [Fact]
    public async Task DecomposeRequestAsync_ModelThrowsTimeout_ReturnsOriginal()
    {
        var modelClient = new DelegatingModelClient((_, _) =>
            throw new TimeoutException("LLM call timed out"));

        var result = await CompoundRequestOrchestrator.DecomposeRequestAsync(
            modelClient, "create file then tell joke", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("create file then tell joke", result[0]);
    }

    [Fact]
    public async Task PlanRequestsAsync_UsesModelDecomposition_ForSemanticCompoundRequest()
    {
        var modelClient = new StubModelClient("[\"Search flights to Berlin\", \"Book a hotel in Berlin\"]");

        var result = await CompoundRequestOrchestrator.PlanRequestsAsync(
            modelClient, "I need to sort out my Berlin trip", CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Search flights to Berlin", result[0]);
        Assert.Equal("Book a hotel in Berlin", result[1]);
    }

    [Fact]
    public async Task PlanRequestsAsync_ReturnsOriginal_WhenNoModelClient()
    {
        var result = await CompoundRequestOrchestrator.PlanRequestsAsync(
            null, "single request", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("single request", result[0]);
    }
}
