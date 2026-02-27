using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace UtilityAi.Compass.Tests;

public sealed class CliPackagingTests
{
    [Fact]
    public async Task DotnetPack_ProducesToolSettingsFileInNupkg()
    {
        var repoRoot = FindRepoRoot();
        var cliProject = Path.Combine(repoRoot, "src", "UtilityAi.Compass.Cli", "UtilityAi.Compass.Cli.csproj");
        var projectDocument = XDocument.Load(cliProject);
        var targetFrameworks = projectDocument
            .Descendants("TargetFrameworks")
            .First()
            .Value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var packageId = projectDocument
            .Descendants("PackageId")
            .First()
            .Value;
        var packageVersion = projectDocument
            .Descendants("Version")
            .First()
            .Value;
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var toolInstallPath = Path.Combine(outputDir, "tools");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(toolInstallPath);

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"pack \"{cliProject}\" -c Debug -o \"{outputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            Assert.NotNull(process);
            using (process)
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;
                Assert.True(process.ExitCode == 0, $"dotnet pack failed with exit code {process.ExitCode}{Environment.NewLine}{output}{Environment.NewLine}{error}");

                var nupkgPath = Directory
                    .GetFiles(outputDir, "UtilityAi.Compass.Cli.*.nupkg")
                    .Single(path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase));

                using var archive = ZipFile.OpenRead(nupkgPath);
                Assert.All(targetFrameworks, framework =>
                    Assert.NotNull(archive.GetEntry($"tools/{framework}/any/DotnetToolSettings.xml")));
            }

            process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"tool install --tool-path \"{toolInstallPath}\" {packageId} --version {packageVersion} --add-source \"{outputDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            Assert.NotNull(process);
            using (process)
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;
                Assert.True(process.ExitCode == 0, $"dotnet tool install failed with exit code {process.ExitCode}{Environment.NewLine}{output}{Environment.NewLine}{error}");
            }

            var toolCommandPath = Path.Combine(toolInstallPath, OperatingSystem.IsWindows() ? "compass.exe" : "compass");
            process = Process.Start(new ProcessStartInfo
            {
                FileName = toolCommandPath,
                Arguments = "--list-modules",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            Assert.NotNull(process);
            using (process)
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                Assert.True(process.ExitCode == 0, $"compass --list-modules failed with exit code {process.ExitCode}{Environment.NewLine}{output}{Environment.NewLine}{error}");
                Assert.Contains("Standard modules:", output);
                Assert.Contains("FileReadModule", output);
                Assert.Contains("No installed modules found.", output);
                Assert.DoesNotContain("Compass CLI started. Type a request", output, StringComparison.Ordinal);
            }

            process = Process.Start(new ProcessStartInfo
            {
                FileName = toolCommandPath,
                Arguments = "-- --list-modules",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            Assert.NotNull(process);
            using (process)
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                Assert.True(process.ExitCode == 0, $"compass -- --list-modules failed with exit code {process.ExitCode}{Environment.NewLine}{output}{Environment.NewLine}{error}");
                Assert.Contains("Standard modules:", output);
                Assert.Contains("FileReadModule", output);
                Assert.Contains("No installed modules found.", output);
                Assert.DoesNotContain("Compass CLI started. Type a request", output, StringComparison.Ordinal);
            }

            process = Process.Start(new ProcessStartInfo
            {
                FileName = toolCommandPath,
                Arguments = "--setup",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            Assert.NotNull(process);
            using (process)
            {
                const string openAiProviderSelection = "1";
                const string apiKey = "test-key";
                const string localConsoleDeploymentSelection = "1";

                await process.StandardInput.WriteLineAsync(openAiProviderSelection);
                await process.StandardInput.WriteLineAsync(apiKey);
                await process.StandardInput.WriteLineAsync(string.Empty);
                await process.StandardInput.WriteLineAsync(localConsoleDeploymentSelection);
                process.StandardInput.Close();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                var output = await outputTask;
                var error = await errorTask;

                Assert.True(process.ExitCode == 0, $"compass --setup failed with exit code {process.ExitCode}{Environment.NewLine}{output}{Environment.NewLine}{error}");
                Assert.Contains("Configuration saved to:", output);
                Assert.Contains("Run: compass", output);
                Assert.DoesNotContain("UtilityAi.Compass.sln", output, StringComparison.Ordinal);
                Assert.DoesNotContain("OpenAI samples enabled.", output, StringComparison.Ordinal);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(outputDir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort test cleanup.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort test cleanup.
            }
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
