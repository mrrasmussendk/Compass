using VitruvianAbstractions;
using VitruvianAbstractions.Interfaces;
using VitruvianAbstractions.Planning;
using VitruvianRuntime.Planning;
using VitruvianRuntime.Routing;

namespace VitruvianRuntime;

/// <summary>
/// GOAP-style request processor that creates a plan before executing.
/// Pipeline: Request → GoapPlanner → [HITL plan review] → PlanExecutor (parallel) → Memory + Cache → Response.
/// </summary>
public sealed class RequestProcessor
{
    private readonly ModuleRouter _router;
    private readonly IModelClient? _modelClient;
    private readonly IApprovalGate? _approvalGate;
    private readonly Dictionary<string, IVitruvianModule> _modules = new();
    private readonly List<(string User, string Assistant)> _conversationHistory = new();
    private readonly GoapPlanner _planner;
    private readonly Action<string>? _logger;
    private readonly Func<IVitruvianModule, IModelClient, IVitruvianModule>? _moduleContextFactory;
    private PlanExecutor? _executor;

    private const int MaxConversationTurns = 10;

    /// <summary>
    /// Initializes a new <see cref="RequestProcessor"/>.
    /// </summary>
    /// <param name="router">The module router used for request routing.</param>
    /// <param name="modelClient">Optional model client for LLM-based planning and fallback.</param>
    /// <param name="approvalGate">Optional HITL gate for write/execute operations.</param>
    /// <param name="logger">Optional log sink; when provided, diagnostic messages are written here instead of being discarded.</param>
    /// <param name="moduleContextFactory">
    /// Optional factory that wraps a module with a context-aware model client.
    /// Receives the original module and a <see cref="ContextAwareModelClient"/> and returns
    /// a replacement module instance. When <c>null</c>, modules are used as-is.
    /// </param>
    public RequestProcessor(
        ModuleRouter router,
        IModelClient? modelClient,
        IApprovalGate? approvalGate = null,
        Action<string>? logger = null,
        Func<IVitruvianModule, IModelClient, IVitruvianModule>? moduleContextFactory = null)
    {
        _router = router;
        _modelClient = modelClient;
        _approvalGate = approvalGate;
        _planner = new GoapPlanner(modelClient);
        _logger = logger;
        _moduleContextFactory = moduleContextFactory;
    }

    /// <summary>Gets the current plan executor (created lazily after the first module is registered).</summary>
    internal PlanExecutor? Executor => _executor;

    /// <summary>Gets the GOAP planner used by this processor.</summary>
    internal GoapPlanner Planner => _planner;

    public void RegisterModule(IVitruvianModule module)
    {
        // Log warning if module doesn't declare permissions (but allow it)
        var requiredAccess = PermissionChecker.GetRequiredAccess(module.GetType());
        if (requiredAccess == ModuleAccess.None)
        {
            Log($"[WARNING] Module '{module.Domain}' does not declare [RequiresPermission] attributes. " +
                            "This is a security best practice. Add [RequiresPermission] to declare intended access levels.");
        }

        // Remove any previous registration to avoid duplicates in router/planner lists
        if (_modules.ContainsKey(module.Domain))
        {
            _router.UnregisterModule(module.Domain);
            _planner.UnregisterModule(module.Domain);
        }

        _modules[module.Domain] = module;

        // Register with router (metadata is optional and defaults to 0.0 cost/risk)
        _router.RegisterModule(module, metadata: null);

        // Register with planner so it knows available capabilities
        _planner.RegisterModule(module.Domain, module.Description);
    }

    /// <summary>
    /// Returns <c>true</c> if a module with the given domain is currently registered.
    /// </summary>
    public bool IsModuleRegistered(string domain)
    {
        return !string.IsNullOrWhiteSpace(domain) && _modules.ContainsKey(domain);
    }

    /// <summary>
    /// Unregisters a module by its domain name so it is no longer available for routing,
    /// planning, or execution.
    /// </summary>
    /// <param name="domain">The domain identifier of the module to remove.</param>
    /// <returns><c>true</c> if a module was found and removed; otherwise <c>false</c>.</returns>
    public bool UnregisterModule(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        if (!_modules.Remove(domain))
            return false;

        _router.UnregisterModule(domain);
        _planner.UnregisterModule(domain);

        // Reset the executor so it is rebuilt with the updated module map
        _executor = null;

        return true;
    }

    /// <summary>
    /// Gets a context-aware version of a module that wraps its model client with conversation history.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the specified domain is not registered.</exception>
    private IVitruvianModule GetContextAwareModule(string domain)
    {
        if (!_modules.TryGetValue(domain, out var module))
            throw new InvalidOperationException($"Module '{domain}' is not registered.");

        // If we don't have a model client or no factory, just return the original module
        if (_modelClient is null || _moduleContextFactory is null)
            return module;

        // Wrap the model client with context
        var contextAwareClient = new ContextAwareModelClient(_modelClient, _conversationHistory);

        // Delegate to the factory to create a context-aware module instance
        return _moduleContextFactory(module, contextAwareClient);
    }

    public async Task<string> ProcessAsync(string input, CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (_modules.Count == 0 && _modelClient is null)
        {
            return "No model configured. Run 'Vitruvian --setup' or scripts/install.sh (Linux/macOS) / scripts/install.ps1 (Windows).";
        }

        // Phase 1: PLAN — create a GOAP-style plan before any execution
        var planStart = sw.ElapsedMilliseconds;
        var plan = await _planner.CreatePlanAsync(BuildEnrichedInput(input), cancellationToken);
        Log($"[GOAP] Plan created: {plan.PlanId} with {plan.Steps.Count} step(s)");
        foreach (var step in plan.Steps)
        {
            Log($"[GOAP]   {step.StepId}: {step.ModuleDomain} — {step.Description} (depends: [{string.Join(", ", step.DependsOn)}])");
        }
        Log($"[PERF] Planning: {sw.ElapsedMilliseconds - planStart}ms");

        // Phase 2: Handle steps with no module (fallback to direct LLM)
        if (plan.Steps.Count == 1 && string.IsNullOrEmpty(plan.Steps[0].ModuleDomain))
        {
            if (_modelClient is null)
                return "No model configured. Run 'Vitruvian --setup' or scripts/install.sh (Linux/macOS) / scripts/install.ps1 (Windows).";

            var fallbackResponse = await _modelClient.GenerateAsync(input, cancellationToken);
            await StoreConversationTurnAsync(input, fallbackResponse, cancellationToken);
            return fallbackResponse;
        }

        // Phase 3: EXECUTE — build context-aware module map and execute via PlanExecutor
        // Reuse the executor across requests to preserve cache and memory state
        if (_executor is null)
        {
            var contextAwareModules = new Dictionary<string, IVitruvianModule>();
            foreach (var kvp in _modules)
            {
                contextAwareModules[kvp.Key] = GetContextAwareModule(kvp.Key);
            }
            _executor = new PlanExecutor(contextAwareModules, _approvalGate);

            // Wire up replanning: when a plan fails, ask the planner to create a revised plan
            if (_modelClient is not null)
            {
                _executor.MaxReplans = 1;
                _executor.ReplanCallback = async (originalRequest, failedResult, ct2) =>
                {
                    Log($"[GOAP] Plan {failedResult.PlanId} failed — attempting replan");
                    return await _planner.ReplanAsync(originalRequest, failedResult, ct2);
                };
            }
        }

        var execStart = sw.ElapsedMilliseconds;
        var result = await _executor.ExecuteAsync(plan, null, cancellationToken);
        Log($"[PERF] Execution: {sw.ElapsedMilliseconds - execStart}ms (parallel steps supported)");

        // Phase 4: MEMORY — store conversation turn and log outcome
        var storeStart = sw.ElapsedMilliseconds;
        await StoreConversationTurnAsync(input, result.AggregatedOutput, cancellationToken);
        Log($"[PERF] StoreConversation: {sw.ElapsedMilliseconds - storeStart}ms");
        Log($"[GOAP] Plan {plan.PlanId} completed: success={result.Success}, memory_size={_executor.Memory.Count}");
        Log($"[PERF] Total: {sw.ElapsedMilliseconds}ms");

        return result.AggregatedOutput;
    }

    private Task StoreConversationTurnAsync(string input, string responseText, CancellationToken cancellationToken)
    {
        // Store in-memory conversation history
        _conversationHistory.Add((input, responseText));

        // Keep only recent turns
        while (_conversationHistory.Count > MaxConversationTurns)
        {
            _conversationHistory.RemoveAt(0);
        }

        return Task.CompletedTask;
    }

    private string BuildEnrichedInput(string input)
    {
        // If there's recent conversation history, provide context to improve routing and execution
        if (_conversationHistory.Count == 0)
            return input;

        // Get the most recent exchange(s) - up to 2 turns for better context
        var recentTurns = _conversationHistory.TakeLast(Math.Min(2, _conversationHistory.Count)).ToList();

        if (recentTurns.Count == 0)
            return input;

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("[Recent conversation context:");

        foreach (var (user, assistant) in recentTurns)
        {
            contextBuilder.AppendLine($"  User: {TruncateForContext(user, 100)}");
            contextBuilder.AppendLine($"  Assistant: {TruncateForContext(assistant, 150)}");
        }

        contextBuilder.AppendLine($"]\n\nCurrent user message: {input}");

        return contextBuilder.ToString();
    }

    private static string TruncateForContext(string text, int maxLength = 200)
    {
        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    private void Log(string message) => _logger?.Invoke(message);
}
