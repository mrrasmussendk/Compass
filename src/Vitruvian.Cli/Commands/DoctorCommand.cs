using System.Text.Json;

namespace VitruvianCli.Commands;

/// <summary>
/// Runs a health check on the Vitruvian installation and reports findings.
/// </summary>
public sealed class DoctorCommand : ICliCommand
{
    private readonly string _pluginsPath;

    public DoctorCommand(string pluginsPath)
    {
        _pluginsPath = pluginsPath;
    }

    public bool CanHandle(string[] args) =>
        args.Length >= 1 &&
        (string.Equals(args[0], "doctor", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(args[0], "--doctor", StringComparison.OrdinalIgnoreCase));

    public Task<int> ExecuteAsync(string[] args)
    {
        var hasUnsigned = ModuleInstaller.ListInstalledModules(_pluginsPath).Any();
        var findings = new List<string>();

        // Run environment validation
        var validationResult = EnvironmentValidator.ValidateEnvironment();
        findings.AddRange(validationResult.MissingVariables);
        findings.AddRange(validationResult.Warnings);

        if (hasUnsigned)
            findings.Add("Installed modules should be inspected with `Vitruvian inspect-module` and signed by default.");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VITRUVIAN_MEMORY_CONNECTION_STRING")))
            findings.Add("Audit store not configured. Set VITRUVIAN_MEMORY_CONNECTION_STRING to SQLite for deterministic audit.");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("VITRUVIAN_SECRET_PROVIDER")))
            findings.Add("Secret provider not configured. Set VITRUVIAN_SECRET_PROVIDER to avoid direct environment-secret usage.");

        var report = new
        {
            Status = findings.Count == 0 ? "healthy" : "needs-attention",
            Findings = findings,
            EnvironmentValid = validationResult.IsValid
        };

        if (args.Any(a => string.Equals(a, "--json", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        else
        {
            Console.WriteLine($"Doctor status: {report.Status}");
            Console.WriteLine($"Environment validation: {(validationResult.IsValid ? "✓ Passed" : "✗ Failed")}");
            Console.WriteLine();
            if (findings.Count > 0)
            {
                Console.WriteLine("Findings:");
                foreach (var finding in findings)
                    Console.WriteLine($"  - {finding}");
            }
            else
            {
                Console.WriteLine("✓ No issues found. Your Vitruvian installation is healthy!");
            }
        }

        return Task.FromResult(0);
    }
}
