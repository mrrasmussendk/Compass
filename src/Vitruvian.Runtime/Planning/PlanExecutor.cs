using System.Collections.Concurrent;
using VitruvianAbstractions.Interfaces;
using VitruvianAbstractions.Planning;
using VitruvianAbstractions;

namespace VitruvianRuntime.Planning;

/// <summary>
/// Executes a GOAP-style <see cref="ExecutionPlan"/> with support for:
/// <list type="bullet">
///   <item>Parallel execution of independent steps (multithreading)</item>
///   <item>Human-in-the-loop gating via <see cref="IApprovalGate"/></item>
///   <item>Per-step result caching</item>
///   <item>Plan outcome memory for future reference</item>
///   <item>Context window management (sliding window of recent results)</item>
///   <item>Precondition / postcondition validation per step</item>
///   <item>Fallback step execution on failure</item>
///   <item>Replanning via an optional callback when the overall plan fails</item>
/// </list>
/// </summary>
public sealed class PlanExecutor
{
    private readonly Dictionary<string, IVitruvianModule> _modules;
    private readonly IApprovalGate? _approvalGate;
    private readonly int _contextWindowSize;

    // In-memory cache: key = (domain + normalised input) → output
    private readonly ConcurrentDictionary<string, string> _cache = new();

    // In-memory durable store of completed plan results
    private readonly ConcurrentBag<PlanResult> _memory = new();

    /// <summary>Gets a snapshot of all remembered plan results.</summary>
    public IReadOnlyList<PlanResult> Memory => _memory.ToArray();

    /// <summary>
    /// Optional replanning callback. When set, the executor calls this function after
    /// a plan execution fails, passing the original request and the failed result.
    /// If the callback returns a new <see cref="ExecutionPlan"/>, the executor runs it
    /// (up to <see cref="MaxReplans"/> times).
    /// </summary>
    public Func<string, PlanResult, CancellationToken, Task<ExecutionPlan?>>? ReplanCallback { get; set; }

    /// <summary>
    /// Maximum number of replan attempts when a plan execution fails. Defaults to 0 (no replanning).
    /// Only effective when <see cref="ReplanCallback"/> is set.
    /// </summary>
    public int MaxReplans { get; set; }

    /// <summary>
    /// Initialises a new <see cref="PlanExecutor"/>.
    /// </summary>
    /// <param name="modules">Map of domain → module instance.</param>
    /// <param name="approvalGate">Optional HITL gate; when provided, write/execute steps require approval.</param>
    /// <param name="contextWindowSize">Max number of prior step outputs injected as context into subsequent steps. Defaults to 4.</param>
    public PlanExecutor(
        Dictionary<string, IVitruvianModule> modules,
        IApprovalGate? approvalGate = null,
        int? contextWindowSize = null)
    {
        _modules = modules;
        _approvalGate = approvalGate;
        _contextWindowSize = contextWindowSize ?? 4;
    }

    /// <summary>
    /// Executes the given plan, respecting dependency edges for parallelism,
    /// gating write/execute steps through HITL, and caching results.
    /// When the plan fails and a <see cref="ReplanCallback"/> is configured,
    /// the executor will attempt to replan up to <see cref="MaxReplans"/> times.
    /// </summary>
    public async Task<PlanResult> ExecuteAsync(ExecutionPlan plan, string? userId, CancellationToken ct)
    {
        var result = await ExecutePlanCoreAsync(plan, userId, ct);

        // Replan loop: when the plan fails and a callback is configured, try to recover
        var replansRemaining = MaxReplans;
        while (!result.Success && replansRemaining > 0 && ReplanCallback is not null)
        {
            replansRemaining--;

            ExecutionPlan? newPlan;
            try
            {
                newPlan = await ReplanCallback(plan.OriginalRequest, result, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                break; // Callback failed — stop replanning
            }

            if (newPlan is null)
                break;

            plan = newPlan;
            result = await ExecutePlanCoreAsync(plan, userId, ct);
        }

        return result;
    }

    private async Task<PlanResult> ExecutePlanCoreAsync(ExecutionPlan plan, string? userId, CancellationToken ct)
    {
        var results = new ConcurrentDictionary<string, PlanStepResult>();
        // Use a list with lock to maintain insertion order for context window
        var stepOutputs = new OrderedOutputs();

        // Collect step IDs that are only used as fallbacks so they are skipped in normal execution
        var fallbackOnlyIds = new HashSet<string>(
            plan.Steps
                .Where(s => !string.IsNullOrEmpty(s.FallbackStepId))
                .Select(s => s.FallbackStepId!),
            StringComparer.Ordinal);

        // Build a lookup for steps by ID (used for fallback resolution)
        var stepLookup = plan.Steps.ToDictionary(s => s.StepId, StringComparer.Ordinal);

        // Group steps into waves: each wave contains steps whose dependencies are already satisfied.
        var remaining = new List<PlanStep>(plan.Steps.Where(s => !fallbackOnlyIds.Contains(s.StepId)));

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(s => s.DependsOn.All(dep => results.ContainsKey(dep)))
                .ToList();

            if (ready.Count == 0)
            {
                // Circular dependency or missing dependency — execute remaining sequentially
                ready = [remaining[0]];
            }

            foreach (var step in ready)
                remaining.Remove(step);

            // Execute the ready wave in parallel
            var tasks = ready.Select(step =>
                ExecuteStepWithFallbackAsync(step, userId, stepOutputs, results, stepLookup, ct));
            var waveResults = await Task.WhenAll(tasks);

            foreach (var result in waveResults)
            {
                results[result.StepId] = result;
                stepOutputs.Add(result.StepId, result.Output);
            }
        }

        // Aggregate — include all steps that were actually executed (primary + triggered fallbacks)
        var orderedResults = plan.Steps
            .Where(s => results.ContainsKey(s.StepId) || !fallbackOnlyIds.Contains(s.StepId))
            .Select(s =>
            {
                if (results.TryGetValue(s.StepId, out var r))
                    return r;
                return new PlanStepResult(s.StepId, s.ModuleDomain, false,
                    "Step not executed", DateTimeOffset.UtcNow, TimeSpan.Zero);
            })
            .ToList();

        var aggregatedOutput = string.Join("\n\n", orderedResults.Select(r => r.Output));
        var planResult = new PlanResult(
            plan.PlanId,
            orderedResults.All(r => r.Success),
            orderedResults,
            aggregatedOutput);

        // Persist to memory
        _memory.Add(planResult);

        return planResult;
    }

    /// <summary>
    /// Executes a step. If the step fails and has a <see cref="PlanStep.FallbackStepId"/>,
    /// the fallback step is executed instead.
    /// </summary>
    private async Task<PlanStepResult> ExecuteStepWithFallbackAsync(
        PlanStep step,
        string? userId,
        OrderedOutputs priorOutputs,
        ConcurrentDictionary<string, PlanStepResult> results,
        Dictionary<string, PlanStep> stepLookup,
        CancellationToken ct)
    {
        var result = await ExecuteStepAsync(step, userId, priorOutputs, results, ct);

        // If the step failed and a fallback is defined, try the fallback
        if (!result.Success
            && !string.IsNullOrEmpty(step.FallbackStepId)
            && stepLookup.TryGetValue(step.FallbackStepId, out var fallbackStep))
        {
            var fallbackResult = await ExecuteStepAsync(fallbackStep, userId, priorOutputs, results, ct);

            // Record the fallback result under the *fallback* step ID so it can serve as a dependency
            results[fallbackStep.StepId] = fallbackResult;
            priorOutputs.Add(fallbackStep.StepId, fallbackResult.Output);

            // Return a result under the *original* step ID so downstream dependents continue to work
            return new PlanStepResult(
                step.StepId,
                fallbackResult.ModuleDomain,
                fallbackResult.Success,
                fallbackResult.Output,
                fallbackResult.ExecutedAt,
                fallbackResult.Duration,
                WasFallback: true);
        }

        return result;
    }

    private async Task<PlanStepResult> ExecuteStepAsync(
        PlanStep step,
        string? userId,
        OrderedOutputs priorOutputs,
        ConcurrentDictionary<string, PlanStepResult> results,
        CancellationToken ct)
    {
        var started = DateTimeOffset.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Precondition check: when a precondition is set, all dependency steps must have succeeded
        if (step.Precondition is not null && step.DependsOn.Count > 0)
        {
            foreach (var depId in step.DependsOn)
            {
                if (results.TryGetValue(depId, out var depResult) && !depResult.Success)
                {
                    return new PlanStepResult(step.StepId, step.ModuleDomain, false,
                        $"Precondition not met: dependency '{depId}' failed — {step.Precondition}",
                        started, sw.Elapsed);
                }
            }
        }

        // Cache check
        var cacheKey = $"{step.ModuleDomain}|{step.Input}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return new PlanStepResult(step.StepId, step.ModuleDomain, true, cached, started, sw.Elapsed);
        }

        // Resolve module
        if (string.IsNullOrEmpty(step.ModuleDomain) || !_modules.TryGetValue(step.ModuleDomain, out var module))
        {
            return new PlanStepResult(step.StepId, step.ModuleDomain, false,
                $"No module found for domain '{step.ModuleDomain}'", started, sw.Elapsed);
        }

        // HITL gating for write/execute operations
        if (_approvalGate is not null)
        {
            var opType = InferOperationType(step);
            if (opType is OperationType.Write or OperationType.Delete or OperationType.Execute)
            {
                var approved = await _approvalGate.ApproveAsync(opType, step.Description, step.ModuleDomain, ct);
                if (!approved)
                {
                    return new PlanStepResult(step.StepId, step.ModuleDomain, false,
                        $"Step denied by human reviewer: {step.Description}", started, sw.Elapsed);
                }
            }
        }

        // Build input with context window from prior steps
        var enrichedInput = BuildContextWindow(step.Input, priorOutputs);

        try
        {
            var output = await module.ExecuteAsync(enrichedInput, userId, ct);
            sw.Stop();

            // Postcondition check: if a postcondition is specified, the output must contain
            // the keyword (case-insensitive)
            if (step.Postcondition is not null
                && !output.Contains(step.Postcondition, StringComparison.OrdinalIgnoreCase))
            {
                return new PlanStepResult(step.StepId, step.ModuleDomain, false,
                    $"Postcondition not met: output does not contain '{step.Postcondition}'. Output: {Truncate(output, 200)}",
                    started, sw.Elapsed);
            }

            // Cache the result
            _cache[cacheKey] = output;

            return new PlanStepResult(step.StepId, step.ModuleDomain, true, output, started, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new PlanStepResult(step.StepId, step.ModuleDomain, false,
                $"Error: {ex.Message}", started, sw.Elapsed);
        }
    }

    private string BuildContextWindow(string input, OrderedOutputs priorOutputs)
    {
        if (priorOutputs.Count == 0 || _contextWindowSize <= 0)
            return input;

        var recentOutputs = priorOutputs.GetRecent(_contextWindowSize);
        if (recentOutputs.Count == 0)
            return input;

        var context = string.Join("\n", recentOutputs.Select(o => Truncate(o, 200)));
        return $"[Prior step context:\n{context}\n]\n\n{input}";
    }

    private static OperationType InferOperationType(PlanStep step)
    {
        var lower = $"{step.Description} {step.Input}".ToLowerInvariant();
        if (lower.Contains("delete") || lower.Contains("remove"))
            return OperationType.Delete;
        if (lower.Contains("execute") || lower.Contains("run") || lower.Contains("shell"))
            return OperationType.Execute;
        if (lower.Contains("write") || lower.Contains("create") || lower.Contains("update") || lower.Contains("modify") || lower.Contains("send"))
            return OperationType.Write;
        return OperationType.Read;
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";

    /// <summary>Thread-safe, insertion-ordered collection of step outputs.</summary>
    private sealed class OrderedOutputs
    {
        private readonly object _lock = new();
        private readonly List<string> _outputs = [];

        public int Count { get { lock (_lock) return _outputs.Count; } }

        public void Add(string stepId, string output)
        {
            lock (_lock) _outputs.Add(output);
        }

        public List<string> GetRecent(int count)
        {
            lock (_lock) return _outputs.TakeLast(count).ToList();
        }
    }
}
