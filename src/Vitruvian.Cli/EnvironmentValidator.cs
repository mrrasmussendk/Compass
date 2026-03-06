namespace VitruvianCli;

/// <summary>
/// Validates that required environment variables and system dependencies are present
/// before the application starts. This helps catch configuration issues early.
/// </summary>
public static class EnvironmentValidator
{
    public sealed record ValidationResult(
        bool IsValid,
        IReadOnlyList<string> MissingVariables,
        IReadOnlyList<string> Warnings)
    {
        public static ValidationResult Success() => new(true, [], []);
        public static ValidationResult Failure(IReadOnlyList<string> missing, IReadOnlyList<string> warnings) =>
            new(false, missing, warnings);
    }

    /// <summary>
    /// Checks for essential environment variables and dependencies on first startup.
    /// Returns a result indicating whether the environment is properly configured.
    /// </summary>
    public static ValidationResult ValidateEnvironment()
    {
        var missing = new List<string>();
        var warnings = new List<string>();

        // Check if .env file exists
        var envFilePath = EnvFileLoader.FindFile([Directory.GetCurrentDirectory(), AppContext.BaseDirectory]);
        if (envFilePath is null)
        {
            warnings.Add("No .env.Vitruvian file found. Run /setup to create configuration.");
        }

        // Check for model provider configuration
        var providerEnv = Environment.GetEnvironmentVariable("VITRUVIAN_MODEL_PROVIDER");
        if (string.IsNullOrWhiteSpace(providerEnv))
        {
            warnings.Add("VITRUVIAN_MODEL_PROVIDER not set. Defaulting to OpenAI.");
        }

        // Check for API keys based on provider
        if (ModelConfiguration.TryParseProvider(providerEnv, out var provider))
        {
            var apiKeyVar = provider switch
            {
                ModelProvider.OpenAi => "OPENAI_API_KEY",
                ModelProvider.Anthropic => "ANTHROPIC_API_KEY",
                ModelProvider.Gemini => "GEMINI_API_KEY",
                _ => null
            };

            if (apiKeyVar is not null)
            {
                var apiKey = Environment.GetEnvironmentVariable(apiKeyVar);
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    missing.Add($"{apiKeyVar} is required for {provider} provider");
                }
            }
        }

        // Check for dotnet SDK (useful for module development)
        if (!IsDotnetSdkAvailable())
        {
            warnings.Add("dotnet SDK not found in PATH. Required for creating custom modules with /new-module.");
        }

        // Check for PowerShell on Windows or bash on Unix (needed for setup script)
        if (OperatingSystem.IsWindows())
        {
            if (!IsCommandAvailable("powershell"))
            {
                warnings.Add("PowerShell not found in PATH. Setup script may not work correctly.");
            }
        }
        else
        {
            if (!IsCommandAvailable("bash"))
            {
                warnings.Add("bash not found in PATH. Setup script may not work correctly.");
            }
        }

        return missing.Count > 0
            ? ValidationResult.Failure(missing, warnings)
            : ValidationResult.Success();
    }

    /// <summary>
    /// Prints validation results to the console with helpful guidance.
    /// </summary>
    public static void PrintValidationResults(ValidationResult result)
    {
        if (result.IsValid)
        {
            if (result.Warnings.Count > 0)
            {
                Console.WriteLine("⚠ Configuration warnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"  - {warning}");
                }
                Console.WriteLine();
            }
            return;
        }

        Console.WriteLine("❌ Environment validation failed:");
        Console.WriteLine();
        foreach (var missing in result.MissingVariables)
        {
            Console.WriteLine($"  ✗ {missing}");
        }
        Console.WriteLine();

        if (result.Warnings.Count > 0)
        {
            Console.WriteLine("Additional warnings:");
            foreach (var warning in result.Warnings)
            {
                Console.WriteLine($"  - {warning}");
            }
            Console.WriteLine();
        }

        Console.WriteLine("To fix this, run:");
        Console.WriteLine("  vitruvian --setup");
        Console.WriteLine();
        Console.WriteLine("Or set environment variables manually:");
        Console.WriteLine("  VITRUVIAN_MODEL_PROVIDER=openai");
        Console.WriteLine("  OPENAI_API_KEY=your-api-key-here");
        Console.WriteLine();
    }

    private static bool IsDotnetSdkAvailable()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null)
                return false;

            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "where" : "which",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null)
                return false;

            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
