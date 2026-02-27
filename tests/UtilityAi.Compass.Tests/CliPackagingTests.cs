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
