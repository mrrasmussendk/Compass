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
        var targetFramework = XDocument.Load(cliProject)
            .Descendants("TargetFramework")
            .First()
            .Value;
        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

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
                Assert.NotNull(archive.GetEntry($"tools/{targetFramework}/any/DotnetToolSettings.xml"));
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
