using System.Diagnostics;
using System.Text.Json;

namespace UtilityAi.Compass.Tests;

public sealed class EnterpriseCliCommandTests
{
    [Fact]
    public async Task PolicyValidate_Succeeds_ForRulesDocument()
    {
        var root = CreateTempDirectory();
        var policyPath = Path.Combine(root, "policy.json");
        await File.WriteAllTextAsync(policyPath, """
            {
              "name": "EnterpriseSafe",
              "rules": [
                { "id": "readonly-allow", "effect": "allow" }
              ]
            }
            """);

        try
        {
            var (exitCode, output, error) = await RunCliAsync($"--policy validate \"{policyPath}\"");

            Assert.Equal(0, exitCode);
            Assert.Contains("Policy validation succeeded.", output);
            Assert.DoesNotContain("failed", output, StringComparison.OrdinalIgnoreCase);
            Assert.True(string.IsNullOrWhiteSpace(error), error);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task Doctor_Json_ContainsStatusAndFindings()
    {
        var (exitCode, output, _) = await RunCliAsync("--doctor --json");

        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(output);
        Assert.True(document.RootElement.TryGetProperty("status", out _));
        Assert.True(document.RootElement.TryGetProperty("findings", out _));
    }

    [Fact]
    public async Task Help_IncludesGettingStartedGuidance()
    {
        var (exitCode, output, error) = await RunCliAsync("--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("Getting started:", output);
        Assert.Contains("compass --setup", output);
        Assert.Contains("type /help for commands or 'quit' to exit", output, StringComparison.OrdinalIgnoreCase);
        Assert.True(string.IsNullOrWhiteSpace(error), error);
    }

    [Fact]
    public async Task InspectModule_Json_ReportsManifestAndCapabilities()
    {
        var root = CreateTempDirectory();
        var pluginDll = Path.Combine(root, "sample-plugin.dll");
        CreateTestPluginDll(pluginDll);
        await File.WriteAllTextAsync(Path.Combine(root, "compass-manifest.json"), """
            {
              "publisher": "tests",
              "version": "1.0.0",
              "capabilities": [ "sample.read" ],
              "permissions": [ "files.read" ],
              "sideEffectLevel": "ReadOnly"
            }
            """);

        try
        {
            var (exitCode, output, error) = await RunCliAsync($"--inspect-module \"{pluginDll}\" --json");

            Assert.Equal(0, exitCode);
            using var document = JsonDocument.Parse(output);
            Assert.True(document.RootElement.GetProperty("hasUtilityAiModule").GetBoolean());
            Assert.True(document.RootElement.GetProperty("hasManifest").GetBoolean());
            Assert.Contains("sample.read", document.RootElement.GetProperty("capabilities").EnumerateArray().Select(v => v.GetString()));
            Assert.True(string.IsNullOrWhiteSpace(error), error);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    [Fact]
    public async Task InstallModule_BlocksUnsignedByDefault()
    {
        var root = CreateTempDirectory();
        var pluginDll = Path.Combine(root, "sample-plugin.dll");
        CreateTestPluginDll(pluginDll);
        await File.WriteAllTextAsync(Path.Combine(root, "compass-manifest.json"), """
            {
              "publisher": "tests",
              "version": "1.0.0",
              "capabilities": [ "sample.read" ],
              "permissions": [ "files.read" ],
              "sideEffectLevel": "ReadOnly"
            }
            """);

        try
        {
            var (exitCode, output, _) = await RunCliAsync($"--install-module \"{pluginDll}\"");

            Assert.NotEqual(0, exitCode);
            Assert.Contains("is unsigned. Re-run with --allow-unsigned to override.", output);
        }
        finally
        {
            TryDeleteDirectory(root);
        }
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunCliAsync(string arguments)
    {
        var repoRoot = FindRepoRoot();
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --framework net10.0 --project \"{Path.Combine(repoRoot, "src", "UtilityAi.Compass.Cli", "UtilityAi.Compass.Cli.csproj")}\" -- {arguments}",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("Failed to start Compass CLI process.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await outputTask, await errorTask);
    }

    private static void CreateTestPluginDll(string targetPath) =>
        File.Copy(typeof(UtilityAi.Compass.StandardModules.FileReadModule).Assembly.Location, targetPath, overwrite: true);

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "compass-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "UtilityAi.Compass.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
