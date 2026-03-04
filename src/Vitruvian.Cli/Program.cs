using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VitruvianCli;
using VitruvianRuntime;
using VitruvianRuntime.Routing;
using VitruvianAbstractions.Facts;
using VitruvianAbstractions.Interfaces;
using VitruvianAbstractions.Scheduling;
using VitruvianPluginHost;
using VitruvianPluginSdk.Attributes;
using VitruvianRuntime.DI;
using VitruvianRuntime.Scheduling;
using VitruvianStandardModules;

// Auto-load .env.Vitruvian so the host works without manually sourcing the file.
EnvFileLoader.Load(overwriteExisting: true);

var pluginsPath = Path.Combine(AppContext.BaseDirectory, "plugins");
void PrintCommands() => Console.WriteLine("Commands: /help, /setup, /list-modules, /install-module <path|package@version> [--allow-unsigned], /unregister-module <domain>, /inspect-module <path|package@version> [--json], /doctor [--json], /policy validate <policyFile>, /policy explain <request>, /audit list, /audit show <id> [--json], /replay <id> [--no-exec], /new-module <Name> [OutputPath], /schedule \"<interval>\" <request>, /list-tasks, /cancel-task <id>, quit");
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
        nameof(VitruvianStandardModules.ConversationModule),
        nameof(VitruvianStandardModules.FileOperationsModule),
        nameof(VitruvianStandardModules.ShellCommandModule),
        nameof(VitruvianStandardModules.SummarizationModule),
        nameof(VitruvianStandardModules.WebSearchModule),
        nameof(VitruvianStandardModules.GmailModule)
    };

    Console.WriteLine($"Standard modules:{Environment.NewLine}  - {string.Join($"{Environment.NewLine}  - ", standardModules)}");

    var installedModules = ModuleInstaller.ListInstalledModules(pluginsPath);
    Console.WriteLine(installedModules.Count == 0
        ? "No installed modules found."
        : $"Installed modules:{Environment.NewLine}  - {string.Join($"{Environment.NewLine}  - ", installedModules)}");
}

Task<int> PrintAuditListAsync()
{
    Console.WriteLine("Audit feature removed with UtilityAI package.");
    Console.WriteLine("Audit functionality was part of the old architecture and is no longer available.");
    return Task.FromResult(1);
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
    Console.WriteLine("Vitruvian CLI arguments:");
    Console.WriteLine("  --help");
    Console.WriteLine("  --setup");
    Console.WriteLine("  --list-modules");
    Console.WriteLine("  --install-module <path|package@version> [--allow-unsigned]");
    Console.WriteLine("  --unregister-module <domain>");
    Console.WriteLine("  --inspect-module <path|package@version> [--json] (alias: inspect-module)");
    Console.WriteLine("  --doctor [--json] (alias: doctor)");
    Console.WriteLine("  --policy validate <policyFile> (alias: policy validate)");
    Console.WriteLine("  --policy explain <request> (alias: policy explain)");
    Console.WriteLine("  --audit list (alias: audit list)");
    Console.WriteLine("  --audit show <id> [--json] (alias: audit show)");
    Console.WriteLine("  --replay <id> [--no-exec] (alias: replay)");
    Console.WriteLine("  --new-module <Name> [OutputPath]");
    Console.WriteLine();
    Console.WriteLine("Getting started:");
    Console.WriteLine("  1) Vitruvian --setup");
    Console.WriteLine("  2) Vitruvian");
    Console.WriteLine("  3) In interactive mode, type /help for commands or 'quit' to exit.");
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
    Console.WriteLine("Restart Vitruvian CLI to load the new module.");
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
        findings.Add("Installed modules should be inspected with `Vitruvian inspect-module` and signed by default.");
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VITRUVIAN_MEMORY_CONNECTION_STRING")))
        findings.Add("Audit store not configured. Set VITRUVIAN_MEMORY_CONNECTION_STRING to SQLite for deterministic audit.");
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VITRUVIAN_SECRET_PROVIDER")))
        findings.Add("Secret provider not configured. Set VITRUVIAN_SECRET_PROVIDER to avoid direct environment-secret usage.");

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
    Console.WriteLine("Audit feature removed with UtilityAI package.");
    Console.WriteLine("Audit functionality was part of the old architecture and is no longer available.");
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
        ? "Vitruvian setup complete."
        : "Vitruvian setup script could not be started. Ensure scripts/install.sh or scripts/install.ps1 exists next to the app.");
    return;
}

string? modelConfigurationError = null;
if (!ModelConfiguration.TryCreateFromEnvironment(out var modelConfiguration, out modelConfigurationError) &&
    EnvFileLoader.FindFile([Directory.GetCurrentDirectory(), AppContext.BaseDirectory]) is null &&
    !Console.IsInputRedirected)
{
    Console.WriteLine("No Vitruvian setup found. Running installer...");
    if (ModuleInstaller.TryRunInstallScript())
    {
        EnvFileLoader.Load(overwriteExisting: true);
        ModelConfiguration.TryCreateFromEnvironment(out modelConfiguration, out modelConfigurationError);
    }
}
if (modelConfiguration is null && !string.IsNullOrWhiteSpace(modelConfigurationError))
    Console.WriteLine($"Model configuration warning: {modelConfigurationError}");

var builder = Host.CreateApplicationBuilder(args);
var memoryConnectionString = Environment.GetEnvironmentVariable("VITRUVIAN_MEMORY_CONNECTION_STRING");

// Set up working directory - defaults to "Vitruvian-workspace" in user's home directory
var workingDirectory = Environment.GetEnvironmentVariable("VITRUVIAN_WORKING_DIRECTORY");
if (string.IsNullOrWhiteSpace(workingDirectory))
{
    var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    workingDirectory = Path.Combine(homeDir, "Vitruvian-workspace");
}

// Ensure working directory exists
if (!Directory.Exists(workingDirectory))
{
    Directory.CreateDirectory(workingDirectory);
    Console.WriteLine($"Created working directory: {workingDirectory}");
}

builder.Services.AddUtilityAiVitruvian(opts =>
{
    opts.EnableGovernanceFinalizer = true;
    opts.EnableHitl = false;
    opts.EnableScheduler = true;
    opts.MemoryConnectionString = memoryConnectionString;
    opts.WorkingDirectory = workingDirectory;
});

// Register the host-level model client so plugins receive it via DI.
// The concrete provider (OpenAI, Anthropic, Gemini) is chosen by env config.
var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
IModelClient? modelClient = modelConfiguration is not null
    ? ModelClientFactory.Create(modelConfiguration, httpClient)
    : null;
if (modelClient is not null)
    builder.Services.AddSingleton<IModelClient>(modelClient);

// Register modules
builder.Services.AddSingleton<IVitruvianModule>(sp =>
    new FileOperationsModule(sp.GetService<IModelClient>(), workingDirectory));
builder.Services.AddSingleton<IVitruvianModule>(sp =>
    new ConversationModule(sp.GetService<IModelClient>()));
builder.Services.AddSingleton<IVitruvianModule>(sp =>
    new WebSearchModule(sp.GetService<IModelClient>()));
builder.Services.AddSingleton<IVitruvianModule>(sp =>
    new SummarizationModule(sp.GetService<IModelClient>()));
builder.Services.AddSingleton<IVitruvianModule>(sp =>
    new GmailModule(sp.GetService<IModelClient>()));
builder.Services.AddSingleton<IVitruvianModule>(sp =>
    new ShellCommandModule(sp.GetService<IModelClient>(), workingDirectory));

// Register module router with configuration options
builder.Services.AddSingleton<ModuleRouter>(sp =>
{
    var options = sp.GetService<Microsoft.Extensions.Options.IOptions<VitruvianOptions>>()?.Value?.Router ?? new RouterOptions();
    return new ModuleRouter(sp.GetService<IModelClient>(), options);
});

var host = builder.Build();

// Create and configure the request processor
var router = host.Services.GetRequiredService<ModuleRouter>();
var approvalGate = new VitruvianHitl.ConsoleApprovalGate(timeout: TimeSpan.FromSeconds(30));
var requestProcessor = new RequestProcessor(host, router, modelClient, approvalGate);

// Register all modules with the router
foreach (var module in host.Services.GetServices<IVitruvianModule>())
{
    requestProcessor.RegisterModule(module);
}

foreach (var module in InstalledModuleLoader.LoadFromPluginsPath(pluginsPath, host.Services))
{
    requestProcessor.RegisterModule(module);
}

var discordToken = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
var discordChannelId = Environment.GetEnvironmentVariable("DISCORD_CHANNEL_ID");
var webSocketUrl = Environment.GetEnvironmentVariable("VITRUVIAN_WEBSOCKET_URL");
var webSocketPublicUrl = Environment.GetEnvironmentVariable("VITRUVIAN_WEBSOCKET_PUBLIC_URL");
var webSocketDomain = Environment.GetEnvironmentVariable("VITRUVIAN_WEBSOCKET_DOMAIN");

// Resolve scheduler services (registered by AddUtilityAiVitruvian when EnableScheduler is true)
var vitruvianOptions = host.Services.GetRequiredService<VitruvianOptions>();
var taskStore = host.Services.GetService<IScheduledTaskStore>();
var scheduleParser = host.Services.GetService<NaturalLanguageScheduleParser>();

// Register the scheduler background service when enabled
if (vitruvianOptions.EnableScheduler && taskStore is not null)
{
    var schedulerService = new SchedulerService(
        taskStore,
        requestProcessor.ProcessAsync,
        vitruvianOptions,
        msg => Console.WriteLine(msg));
    _ = schedulerService.StartAsync(CancellationToken.None);
}

if (!string.IsNullOrWhiteSpace(webSocketUrl))
{
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var bridge = new WebSocketChannelBridge(webSocketUrl, webSocketPublicUrl, webSocketDomain);
    await bridge.RunAsync(async (request, token) =>
    {
        var response = await requestProcessor.ProcessAsync(request, token);
        return response;
    }, cts.Token);
}
else if (!string.IsNullOrWhiteSpace(discordToken) && !string.IsNullOrWhiteSpace(discordChannelId))
{
    Console.WriteLine("Vitruvian CLI started in Discord mode.");
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };

    var bridge = new DiscordChannelBridge(httpClient, discordToken, discordChannelId);
    await bridge.RunAsync(async (message, token) =>
    {
        var response = await requestProcessor.ProcessAsync(message, token);
        return response;
    }, cts.Token);
}
else
{
    Console.WriteLine("Vitruvian CLI started. Type a request (or 'quit' to exit):");
    PrintCommands();
    if (modelConfiguration is not null)
        Console.WriteLine($"Model provider configured: {modelConfiguration.Provider} ({modelConfiguration.Model})");
    Console.WriteLine($"Working directory: {workingDirectory}");

    var cliService = new CliHostedService(
        requestProcessor,
        host.Services.GetRequiredService<IHostApplicationLifetime>(),
        PrintCommands,
        PrintInstalledModules,
        async (spec, unsigned) =>
        {
            var installResult = await ModuleInstaller.InstallWithResultAsync(spec, pluginsPath, unsigned, PromptForSecret);
            Console.WriteLine($"  {installResult.Message}");
            if (installResult.Success)
            {
                var loaded = InstalledModuleLoader.LoadFromPluginsPath(pluginsPath, host.Services);
                var registered = 0;
                foreach (var module in loaded)
                {
                    if (!requestProcessor.IsModuleRegistered(module.Domain))
                    {
                        requestProcessor.RegisterModule(module);
                        registered++;
                    }
                }

                Console.WriteLine(registered > 0
                    ? $"  {registered} new module(s) loaded and ready to use."
                    : "  Module(s) loaded and ready to use.");
            }
        },
        domain => requestProcessor.UnregisterModule(domain),
        ModuleInstaller.ScaffoldNewModule,
        taskStore,
        scheduleParser);

    // Register the CLI service as a hosted service and run the host.
    // The host manages the CLI lifecycle alongside any other background services (e.g. scheduler).
    await cliService.StartAsync(CancellationToken.None);

    // Wait until the application is signalled to stop (by CliHostedService calling StopApplication or Ctrl+C)
    var tcs = new TaskCompletionSource();
    host.Services.GetRequiredService<IHostApplicationLifetime>()
        .ApplicationStopping.Register(() => tcs.TrySetResult());
    await tcs.Task;

    await cliService.StopAsync(CancellationToken.None);
}

Console.WriteLine("Vitruvian CLI stopped.");
