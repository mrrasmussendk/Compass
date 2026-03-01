using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Compass.SampleHost;
using UtilityAi.Capabilities;
using UtilityAi.Compass.Runtime.Memory;
using UtilityAi.Memory;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.PluginHost;
using UtilityAi.Compass.PluginSdk.Attributes;
using UtilityAi.Compass.PluginSdk.MetadataProvider;
using UtilityAi.Compass.Runtime.DI;
using UtilityAi.Compass.Runtime.Sensors;
using UtilityAi.Compass.StandardModules.DI;
using UtilityAi.Orchestration;
using UtilityAi.Sensor;
using UtilityAi.Utils;

// Auto-load .env.compass so the host works without manually sourcing the file.
EnvFileLoader.Load(overwriteExisting: true);

var pluginsPath = Path.Combine(AppContext.BaseDirectory, "plugins");
const int MaxAuditRecords = 100;
void PrintCommands() => Console.WriteLine("Commands: /help, /setup, /list-modules, /install-module <path|package@version> [--allow-unsigned], /inspect-module <path|package@version> [--json], /doctor [--json], /policy validate <policyFile>, /policy explain <request>, /audit list, /audit show <id> [--json], /replay <id> [--no-exec], /new-module <Name> [OutputPath]");
string? PromptForSecret(string secretName)
{
    Console.Write($"Missing required secret '{secretName}'. Enter value (blank will fail install): ");
    var value = ReadSecretFromConsole();
    Console.WriteLine();
    return value;
}

static string ReadSecretFromConsole()
{
    if (Console.IsInputRedirected)
        return Console.ReadLine() ?? string.Empty;

    var buffer = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
            return new string(buffer.ToArray());

        if (key.Key == ConsoleKey.Backspace)
        {
            if (buffer.Count == 0)
                continue;
            buffer.RemoveAt(buffer.Count - 1);
            Console.Write("\b \b");
            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            buffer.Add(key.KeyChar);
            Console.Write('*');
        }
    }
}
void PrintInstalledModules()
{
    var standardModules = new[]
    {
        nameof(UtilityAi.Compass.StandardModules.FileReadModule),
        nameof(UtilityAi.Compass.StandardModules.FileCreationModule),
        nameof(UtilityAi.Compass.StandardModules.SummarizationModule),
        nameof(UtilityAi.Compass.StandardModules.WebSearchModule),
        nameof(UtilityAi.Compass.StandardModules.GmailModule)
    };

    Console.WriteLine($"Standard modules:{Environment.NewLine}  - {string.Join($"{Environment.NewLine}  - ", standardModules)}");

    var installedModules = ModuleInstaller.ListInstalledModules(pluginsPath);
    Console.WriteLine(installedModules.Count == 0
        ? "No installed modules found."
        : $"Installed modules:{Environment.NewLine}  - {string.Join($"{Environment.NewLine}  - ", installedModules)}");
}

async Task<int> PrintAuditListAsync()
{
    var store = CreateAuditStore();
    if (store is null)
    {
        Console.WriteLine("Audit unavailable: set COMPASS_MEMORY_CONNECTION_STRING to a SQLite connection string.");
        return 1;
    }

    var records = await store.RecallAsync<ProposalExecutionRecord>(new MemoryQuery { MaxResults = MaxAuditRecords, SortOrder = SortOrder.NewestFirst });
    if (records.Count == 0)
    {
        Console.WriteLine("No audit records found.");
        return 0;
    }

    foreach (var entry in records.Select((record, i) => new { Record = record, Index = i + 1 }))
        Console.WriteLine($"{entry.Index}: {entry.Record.Timestamp:O} {entry.Record.Fact.ProposalId} corr={entry.Record.Fact.CorrelationId ?? "n/a"}");

    return 0;
}

SqliteMemoryStore? CreateAuditStore()
{
    var connectionString = Environment.GetEnvironmentVariable("COMPASS_MEMORY_CONNECTION_STRING");
    if (string.IsNullOrWhiteSpace(connectionString))
        return null;
    return new SqliteMemoryStore(connectionString);
}

var startupArgs = args
    .Where(arg => !string.IsNullOrWhiteSpace(arg))
    .Select(arg => arg.Trim())
    .ToArray();
if (startupArgs.Length >= 1 && string.Equals(startupArgs[0], "--", StringComparison.Ordinal))
    startupArgs = startupArgs[1..];

if (startupArgs.Length >= 1 &&
    (string.Equals(startupArgs[0], "--help", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "/help", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine("Compass CLI arguments:");
    Console.WriteLine("  --help");
    Console.WriteLine("  --setup");
    Console.WriteLine("  --list-modules");
    Console.WriteLine("  --install-module <path|package@version> [--allow-unsigned]");
    Console.WriteLine("  --inspect-module <path|package@version> [--json] (alias: inspect-module)");
    Console.WriteLine("  --doctor [--json] (alias: doctor)");
    Console.WriteLine("  --policy validate <policyFile> (alias: policy validate)");
    Console.WriteLine("  --policy explain <request> (alias: policy explain)");
    Console.WriteLine("  --audit list (alias: audit list)");
    Console.WriteLine("  --audit show <id> [--json] (alias: audit show)");
    Console.WriteLine("  --replay <id> [--no-exec] (alias: replay)");
    Console.WriteLine("  --new-module <Name> [OutputPath]");
    return;
}

if (startupArgs.Length >= 1 &&
    (string.Equals(startupArgs[0], "--list-modules", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "/list-modules", StringComparison.OrdinalIgnoreCase)))
{
    PrintInstalledModules();
    return;
}

if (startupArgs.Length >= 2 &&
    (string.Equals(startupArgs[0], "--install-module", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "/install-module", StringComparison.OrdinalIgnoreCase)))
{
    var allowUnsigned = startupArgs.Any(a => string.Equals(a, "--allow-unsigned", StringComparison.OrdinalIgnoreCase));
    var installResult = await ModuleInstaller.InstallWithResultAsync(startupArgs[1], pluginsPath, allowUnsigned, PromptForSecret);
    Console.WriteLine(installResult.Message);
    if (!installResult.Success)
        Environment.ExitCode = 1;
    Console.WriteLine("Restart Compass CLI to load the new module.");
    return;
}

if (startupArgs.Length >= 2 &&
    (string.Equals(startupArgs[0], "inspect-module", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "--inspect-module", StringComparison.OrdinalIgnoreCase)))
{
    var report = await ModuleInstaller.InspectAsync(startupArgs[1]);
    var asJson = startupArgs.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase));
    if (asJson)
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    else
    {
        Console.WriteLine(report.Summary);
        Console.WriteLine($"  hasModule: {report.HasUtilityAiModule}");
        Console.WriteLine($"  signed: {report.IsSigned}");
        Console.WriteLine($"  manifest: {report.HasManifest}");
        if (report.Capabilities.Count > 0)
            Console.WriteLine($"  capabilities: {string.Join(", ", report.Capabilities)}");
        if (report.Permissions.Count > 0)
            Console.WriteLine($"  permissions: {string.Join(", ", report.Permissions)}");
        if (report.Findings.Count > 0)
            Console.WriteLine($"  findings: {string.Join(" | ", report.Findings)}");
    }
    return;
}

if (startupArgs.Length >= 1 &&
    (string.Equals(startupArgs[0], "doctor", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "--doctor", StringComparison.OrdinalIgnoreCase)))
{
    var hasUnsigned = ModuleInstaller.ListInstalledModules(pluginsPath).Any();
    var findings = new List<string>();
    if (hasUnsigned)
        findings.Add("Installed modules should be inspected with `compass inspect-module` and signed by default.");
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COMPASS_MEMORY_CONNECTION_STRING")))
        findings.Add("Audit store not configured. Set COMPASS_MEMORY_CONNECTION_STRING to SQLite for deterministic audit.");
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COMPASS_SECRET_PROVIDER")))
        findings.Add("Secret provider not configured. Set COMPASS_SECRET_PROVIDER to avoid direct environment-secret usage.");

    var report = new
    {
        Status = findings.Count == 0 ? "healthy" : "needs-attention",
        Findings = findings
    };
    if (startupArgs.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    else
    {
        Console.WriteLine($"Doctor status: {report.Status}");
        foreach (var finding in report.Findings)
            Console.WriteLine($"  - {finding}");
    }
    return;
}

if (startupArgs.Length >= 3 &&
    (string.Equals(startupArgs[0], "policy", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "--policy", StringComparison.OrdinalIgnoreCase)) &&
    string.Equals(startupArgs[1], "validate", StringComparison.OrdinalIgnoreCase))
{
    var policyPath = startupArgs[2];
    if (!File.Exists(policyPath))
    {
        Console.WriteLine($"Policy validation failed: '{policyPath}' not found.");
        return;
    }

    try
    {
        using var stream = File.OpenRead(policyPath);
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        var hasRules = root.TryGetProperty("rules", out var rules) && rules.ValueKind == JsonValueKind.Array;
        Console.WriteLine(hasRules
            ? "Policy validation succeeded."
            : "Policy validation failed: expected top-level JSON array property 'rules'.");
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"Policy validation failed: {ex.Message}");
    }
    return;
}

if (startupArgs.Length >= 3 &&
    (string.Equals(startupArgs[0], "policy", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "--policy", StringComparison.OrdinalIgnoreCase)) &&
    string.Equals(startupArgs[1], "explain", StringComparison.OrdinalIgnoreCase))
{
    var request = string.Join(' ', startupArgs.Skip(2));
    var requiresApproval = request.Contains("delete", StringComparison.OrdinalIgnoreCase)
        || request.Contains("write", StringComparison.OrdinalIgnoreCase)
        || request.Contains("update", StringComparison.OrdinalIgnoreCase);
    Console.WriteLine(requiresApproval
        ? "Policy explain: matched EnterpriseSafe write/destructive guard; approval required."
        : "Policy explain: matched EnterpriseSafe readonly allow rule.");
    return;
}

if (startupArgs.Length >= 2 &&
    (string.Equals(startupArgs[0], "audit", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "--audit", StringComparison.OrdinalIgnoreCase)) &&
    string.Equals(startupArgs[1], "list", StringComparison.OrdinalIgnoreCase))
{
    await PrintAuditListAsync();
    return;
}

if (startupArgs.Length >= 3 &&
    (string.Equals(startupArgs[0], "audit", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "--audit", StringComparison.OrdinalIgnoreCase)) &&
    string.Equals(startupArgs[1], "show", StringComparison.OrdinalIgnoreCase))
{
    var store = CreateAuditStore();
    if (store is null)
    {
        Console.WriteLine("Audit unavailable: set COMPASS_MEMORY_CONNECTION_STRING to a SQLite connection string.");
        return;
    }
    if (!int.TryParse(startupArgs[2], out var id) || id <= 0)
    {
        Console.WriteLine("Audit show failed: id must be a positive integer from `compass audit list`.");
        return;
    }
    var records = await store.RecallAsync<ProposalExecutionRecord>(new MemoryQuery { MaxResults = MaxAuditRecords, SortOrder = SortOrder.NewestFirst });
    if (id > records.Count)
    {
        Console.WriteLine("Audit show failed: id not found.");
        return;
    }
    var selected = records[id - 1];
    if (startupArgs.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine(JsonSerializer.Serialize(selected, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    else
        Console.WriteLine($"{selected.Timestamp:O} proposal={selected.Fact.ProposalId} corr={selected.Fact.CorrelationId ?? "n/a"} outcome={selected.Fact.OutcomeTag?.ToString() ?? "n/a"}");
    return;
}

if (startupArgs.Length >= 2 &&
    (string.Equals(startupArgs[0], "replay", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "--replay", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine($"Replay accepted for audit id '{startupArgs[1]}'.");
    Console.WriteLine(startupArgs.Any(a => string.Equals(a, "--no-exec", StringComparison.OrdinalIgnoreCase))
        ? "Replay mode: selection-only (no side effects)."
        : "Replay mode: side effects disabled by default in this build.");
    return;
}

if (startupArgs.Length >= 2 &&
    (string.Equals(startupArgs[0], "--new-module", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "/new-module", StringComparison.OrdinalIgnoreCase)))
{
    var outputPath = startupArgs.Length >= 3 ? startupArgs[2] : Directory.GetCurrentDirectory();
    Console.WriteLine(ModuleInstaller.ScaffoldNewModule(startupArgs[1], outputPath));
    return;
}

if (startupArgs.Length >= 1 &&
    (string.Equals(startupArgs[0], "--setup", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(startupArgs[0], "/setup", StringComparison.OrdinalIgnoreCase)))
{
    Console.WriteLine(ModuleInstaller.TryRunInstallScript()
        ? "Compass setup complete."
        : "Compass setup script could not be started. Ensure scripts/install.sh or scripts/install.ps1 exists next to the app.");
    return;
}

if (!ModelConfiguration.TryCreateFromEnvironment(out var modelConfiguration) &&
    EnvFileLoader.FindFile([Directory.GetCurrentDirectory(), AppContext.BaseDirectory]) is null &&
    !Console.IsInputRedirected)
{
    Console.WriteLine("No Compass setup found. Running installer...");
    if (ModuleInstaller.TryRunInstallScript())
    {
        EnvFileLoader.Load(overwriteExisting: true);
        ModelConfiguration.TryCreateFromEnvironment(out modelConfiguration);
    }
}

var builder = Host.CreateApplicationBuilder(args);
var memoryConnectionString = Environment.GetEnvironmentVariable("COMPASS_MEMORY_CONNECTION_STRING");

builder.Services.AddUtilityAiCompass(opts =>
{
    opts.EnableGovernanceFinalizer = true;
    opts.EnableHitl = false;
    opts.MemoryConnectionString = memoryConnectionString;
});

builder.Services.AddCompassStandardModules();

builder.Services.AddSingleton<AttributeMetadataProvider>();
builder.Services.AddSingleton<IProposalMetadataProvider>(sp => sp.GetRequiredService<AttributeMetadataProvider>());

// Register the host-level model client so plugins receive it via DI.
// The concrete provider (OpenAI, Anthropic, Gemini) is chosen by env config.
var httpClient = new HttpClient();
IModelClient? modelClient = modelConfiguration is not null
    ? ModelClientFactory.Create(modelConfiguration, httpClient)
    : null;
if (modelClient is not null)
    builder.Services.AddSingleton<IModelClient>(modelClient);

builder.Services.AddCompassPluginsFromFolder(pluginsPath);

var host = builder.Build();

var strategy = host.Services.GetRequiredService<UtilityAi.Compass.Runtime.Strategy.CompassGovernedSelectionStrategy>();
var metadataProvider = host.Services.GetRequiredService<AttributeMetadataProvider>();

// Register all module types with the metadata provider so their attributes can be read
// We use a wildcard pattern - any proposal starting with the domain will use this module's metadata
foreach (var module in host.Services.GetServices<ICapabilityModule>())
{
    var moduleType = module.GetType();
    var capabilityAttr = moduleType.GetCustomAttribute<CompassCapabilityAttribute>();
    if (capabilityAttr is not null)
    {
        // Register with domain + "." so it matches proposals like "file-creation.write", "weather-web.current", etc.
        metadataProvider.RegisterModuleType(capabilityAttr.Domain + ".", moduleType);
    }
}

async Task<(GoalSelected? Goal, LaneSelected? Lane, string Response)> ProcessRequestAsync(string input, CancellationToken cancellationToken)
{
    var bus = new EventBus();
    bus.Publish(new UserRequest(input));

    // Create orchestrator with the governance strategy and the request-specific bus
    var requestOrchestrator = new UtilityAiOrchestrator(selector: strategy, stopAtZero: true, bus: bus);

    // Add all sensors
    foreach (var sensor in host.Services.GetServices<ISensor>())
        requestOrchestrator.AddSensor(sensor);

    // Add all modules
    foreach (var module in host.Services.GetServices<ICapabilityModule>())
        requestOrchestrator.AddModule(module);

    await requestOrchestrator.RunAsync(maxTicks: 1, cancellationToken);

    var goal = bus.GetOrDefault<GoalSelected>();
    var lane = bus.GetOrDefault<LaneSelected>();
    var response = bus.GetOrDefault<AiResponse>();
    if (response is not null)
        return (goal, lane, response.Text);

    if (modelClient is null)
        return (goal, lane, "No model configured. Run 'compass --setup' or scripts/install.sh (Linux/macOS) / scripts/install.ps1 (Windows).");

    var modelResponse = await modelClient.GenerateAsync(input, cancellationToken);
    return (goal, lane, modelResponse);
}

var discordToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
var discordChannelId = Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID");
if (!string.IsNullOrWhiteSpace(discordToken) && !string.IsNullOrWhiteSpace(discordChannelId))
{
    Console.WriteLine("Compass CLI started in Discord mode.");
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var bridge = new DiscordChannelBridge(httpClient, discordToken, discordChannelId);
    await bridge.RunAsync(async (message, token) =>
    {
        var (_, _, response) = await ProcessRequestAsync(message, token);
        return response;
    }, cts.Token);
}
else
{
    Console.WriteLine("Compass CLI started. Type a request (or 'quit' to exit):");
    PrintCommands();
    if (modelConfiguration is not null)
        Console.WriteLine($"Model provider configured: {modelConfiguration.Provider} ({modelConfiguration.Model})");

    while (true)
    {
        Console.Write("> ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input) || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            break;

        if (string.Equals(input.Trim(), "/help", StringComparison.OrdinalIgnoreCase))
        {
            PrintCommands();
            continue;
        }

        if (string.Equals(input.Trim(), "/setup", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(ModuleInstaller.TryRunInstallScript()
                ? "Compass setup complete."
                : "Compass setup script could not be started. Ensure scripts/install.sh or scripts/install.ps1 exists next to the app.");
            continue;
        }

        if (string.Equals(input.Trim(), "/list-modules", StringComparison.OrdinalIgnoreCase))
        {
            PrintInstalledModules();
            continue;
        }

        if (ModuleInstaller.TryParseInstallCommand(input, out var moduleSpec, out var allowUnsigned))
        {
            var installResult = await ModuleInstaller.InstallWithResultAsync(moduleSpec, pluginsPath, allowUnsigned, PromptForSecret);
            Console.WriteLine($"  {installResult.Message}");
            Console.WriteLine("  Restart Compass CLI to load the new module.");
            continue;
        }

        if (ModuleInstaller.TryParseNewModuleCommand(input, out var moduleName, out var outputPath))
        {
            Console.WriteLine($"  {ModuleInstaller.ScaffoldNewModule(moduleName, outputPath)}");
            continue;
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var (goal, lane, responseText) = await ProcessRequestAsync(input, cts.Token);

            Console.WriteLine($"  Goal: {goal?.Goal} ({goal?.Confidence:P0}), Lane: {lane?.Lane}");
            Console.WriteLine($"  Response: {responseText}");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
        }
    }
}

Console.WriteLine("Compass CLI stopped.");
