using System.Text.Json;
using VitruvianAbstractions;
using VitruvianAbstractions.Interfaces;
using VitruvianAbstractions.Planning;

namespace VitruvianRuntime.Planning;

/// <summary>
/// GOAP-style planner that produces an <see cref="ExecutionPlan"/> from a user request
/// and the set of available modules. The plan is generated <em>before</em> any execution
/// begins, allowing HITL review, caching, and parallel scheduling.
/// </summary>
public sealed class GoapPlanner
{
    private readonly IModelClient? _modelClient;
    private readonly List<ModuleInfo> _modules = [];

    /// <summary>
    /// Gets or sets an optional custom system prompt template for the planner.
    /// Use <c>{modules}</c> as a placeholder for the available module list.
    /// When <c>null</c>, the built-in default prompt is used.
    /// </summary>
    public string? PlannerPromptTemplate { get; set; }

    public GoapPlanner(IModelClient? modelClient = null)
    {
        _modelClient = modelClient;
    }

    /// <summary>Registers a module so the planner knows it is available.</summary>
    public void RegisterModule(string domain, string description)
    {
        _modules.Add(new ModuleInfo(domain, description));
    }

    /// <summary>
    /// Unregisters a module by its domain name so it is no longer available for planning.
    /// </summary>
    /// <param name="domain">The domain identifier of the module to remove.</param>
    /// <returns><c>true</c> if a module was found and removed; otherwise <c>false</c>.</returns>
    public bool UnregisterModule(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        return _modules.RemoveAll(m => m.Domain == domain) > 0;
    }

    /// <summary>
    /// Creates a GOAP-style execution plan for the given user request.
    /// When no model client is available, falls back to a single-step plan
    /// using keyword-based module matching.
    /// </summary>
    public async Task<ExecutionPlan> CreatePlanAsync(string request, CancellationToken ct)
    {
        var planId = Guid.NewGuid().ToString("N")[..12];

        if (_modules.Count == 0)
            return SingleStepPlan(planId, request, domain: null);

        if (_modelClient is null)
            return SingleStepPlan(planId, request, FallbackDomain(request));

        try
        {
            var systemPrompt = BuildPlannerPrompt();
            var userPrompt = $"User request: {request}\n\nProduce a plan as a JSON array. Each element: {{\"step_id\":\"s1\",\"module\":\"<domain>\",\"description\":\"<what>\",\"input\":\"<request text>\",\"depends_on\":[],\"complexity\":\"low|medium|high\",\"precondition\":\"<optional condition that must hold before this step runs>\",\"postcondition\":\"<optional keyword/phrase that must appear in the output>\",\"fallback_step_id\":\"<optional step_id of a fallback step>\"}}. Independent steps should have empty depends_on so they can run in parallel. The complexity field indicates how complex the step is: \"low\" for simple lookups or direct responses, \"medium\" for moderate reasoning, \"high\" for deep analysis or creative tasks. Return ONLY valid JSON.";

            var response = await _modelClient.CompleteAsync(systemPrompt, userPrompt, cancellationToken: ct);
            var plan = ParsePlanResponse(planId, request, response);
            if (plan is not null)
                return plan;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Log and fall through to single-step fallback
            Console.WriteLine($"[PLANNER] Plan creation failed: {ex.Message}. Falling back to single-step plan.");
        }

        return SingleStepPlan(planId, request, FallbackDomain(request));
    }

    /// <summary>
    /// Creates a new plan that accounts for the failures observed in a previous plan result.
    /// When no model client is available, falls back to a single-step plan using keyword matching.
    /// </summary>
    /// <param name="originalRequest">The original user request.</param>
    /// <param name="failedResult">The result of the failed plan execution.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A new <see cref="ExecutionPlan"/> that avoids the failures.</returns>
    public async Task<ExecutionPlan> ReplanAsync(string originalRequest, PlanResult failedResult, CancellationToken ct)
    {
        var planId = Guid.NewGuid().ToString("N")[..12];

        if (_modules.Count == 0)
            return SingleStepPlan(planId, originalRequest, domain: null);

        if (_modelClient is null)
            return SingleStepPlan(planId, originalRequest, FallbackDomain(originalRequest));

        try
        {
            var systemPrompt = BuildPlannerPrompt();
            var failureSummary = BuildFailureSummary(failedResult);
            var userPrompt = $"The previous plan failed. Here is what happened:\n{failureSummary}\n\nOriginal user request: {originalRequest}\n\nProduce a revised plan as a JSON array that avoids the failures above. Use different modules or approaches where the previous plan failed. Each element: {{\"step_id\":\"s1\",\"module\":\"<domain>\",\"description\":\"<what>\",\"input\":\"<request text>\",\"depends_on\":[],\"complexity\":\"low|medium|high\",\"precondition\":\"<optional condition>\",\"postcondition\":\"<optional expected output keyword>\",\"fallback_step_id\":\"<optional fallback step>\"}}. Return ONLY valid JSON.";

            var response = await _modelClient.CompleteAsync(systemPrompt, userPrompt, cancellationToken: ct);
            var plan = ParsePlanResponse(planId, originalRequest, response);
            if (plan is not null)
                return plan;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PLANNER] Replan failed: {ex.Message}. Falling back to single-step plan.");
        }

        return SingleStepPlan(planId, originalRequest, FallbackDomain(originalRequest));
    }

    // ------------------------------------------------------------------
    // Internals
    // ------------------------------------------------------------------

    internal string BuildPlannerPrompt()
    {
        var moduleList = string.Join("\n", _modules.Select(m => $"- {m.Domain}: {m.Description}"));

        if (PlannerPromptTemplate is not null)
            return PlannerPromptTemplate.Replace("{modules}", moduleList);

        return $"""
            You are a GOAP (Goal-Oriented Action Planning) planner.
            Given a user request and the available modules below, produce an execution plan
            as an ordered list of steps.  Each step targets exactly one module.

            Available modules:
            {moduleList}

            Rules:
            1. If the request can be satisfied by a single module, return a single-step plan.
            2. If the request requires multiple modules, break it into the minimal set of steps.
            3. Mark steps that do NOT depend on earlier output with an empty "depends_on" array
               so they can execute in parallel.
            4. Steps that need output from a prior step must list its step_id in "depends_on".
            5. Always include a brief "description" of what the step accomplishes.
            6. The "input" field should be a self-contained request the module can execute.
            7. Assign a "complexity" to each step: "low" for simple lookups or direct responses,
               "medium" for moderate reasoning or composition, "high" for deep analysis or creative tasks.
            8. Optionally add a "precondition" describing what must be true before the step runs.
               When a precondition is set, every dependency step must have succeeded or the step
               will be skipped.
            9. Optionally add a "postcondition" — a keyword or phrase that must appear in the
               step output for it to be considered successful.
            10. Optionally add a "fallback_step_id" referencing another step that should execute
                only if this step fails. The fallback step must also be in the plan array and will
                be skipped during normal execution.

            Return ONLY a JSON array, no markdown fences or extra text.
            """;
    }

    private ExecutionPlan? ParsePlanResponse(string planId, string originalRequest, string response)
    {
        var json = JsonUtilities.CleanMarkdownCodeFences(response);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        var steps = new List<PlanStep>();
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var stepId = el.TryGetProperty("step_id", out var sid) ? sid.GetString() ?? $"s{steps.Count + 1}" : $"s{steps.Count + 1}";
            var module = el.TryGetProperty("module", out var mod) ? mod.GetString() ?? "" : "";
            var desc = el.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var input = el.TryGetProperty("input", out var inp) ? inp.GetString() ?? originalRequest : originalRequest;
            var deps = new List<string>();
            if (el.TryGetProperty("depends_on", out var depArr) && depArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var dep in depArr.EnumerateArray())
                {
                    var depStr = dep.GetString();
                    if (!string.IsNullOrEmpty(depStr))
                        deps.Add(depStr);
                }
            }

            // Validate that the module exists; skip unknown modules.
            if (!_modules.Any(m => m.Domain == module))
                continue;

            // Parse optional complexity hint
            Complexity? complexity = null;
            if (el.TryGetProperty("complexity", out var comp) && comp.ValueKind == JsonValueKind.String)
            {
                var compStr = comp.GetString();
                if (Enum.TryParse<Complexity>(compStr, ignoreCase: true, out var parsed))
                    complexity = parsed;
            }

            // Parse optional precondition
            string? precondition = null;
            if (el.TryGetProperty("precondition", out var pre) && pre.ValueKind == JsonValueKind.String)
                precondition = pre.GetString();

            // Parse optional postcondition
            string? postcondition = null;
            if (el.TryGetProperty("postcondition", out var post) && post.ValueKind == JsonValueKind.String)
                postcondition = post.GetString();

            // Parse optional fallback step ID
            string? fallbackStepId = null;
            if (el.TryGetProperty("fallback_step_id", out var fb) && fb.ValueKind == JsonValueKind.String)
                fallbackStepId = fb.GetString();

            steps.Add(new PlanStep(stepId, module, desc, input, deps, complexity,
                precondition, postcondition, fallbackStepId));
        }

        return steps.Count > 0
            ? new ExecutionPlan(planId, originalRequest, steps)
            : null;
    }

    private ExecutionPlan SingleStepPlan(string planId, string request, string? domain)
    {
        var step = new PlanStep(
            StepId: "s1",
            ModuleDomain: domain ?? "",
            Description: "Execute request",
            Input: request,
            DependsOn: []);

        return new ExecutionPlan(planId, request, [step]);
    }

    private string? FallbackDomain(string request)
    {
        var best = _modules
            .Select(m => new { m.Domain, Score = TextMatchingUtilities.CalculateMatchScore(request, m.Description) })
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return best?.Score > 0 ? best.Domain : _modules.FirstOrDefault()?.Domain;
    }

    private static string BuildFailureSummary(PlanResult failedResult)
    {
        var lines = new List<string>();
        foreach (var sr in failedResult.StepResults)
        {
            var status = sr.Success ? "succeeded" : "FAILED";
            lines.Add($"- Step {sr.StepId} ({sr.ModuleDomain}): {status} — {Truncate(sr.Output, 150)}");
        }
        return string.Join("\n", lines);
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";

    private sealed record ModuleInfo(string Domain, string Description);
}
