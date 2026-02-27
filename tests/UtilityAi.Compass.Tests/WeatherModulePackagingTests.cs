using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;

namespace UtilityAi.Compass.Tests;

public sealed class WeatherModulePackagingTests
{
    [Fact]
    public async Task DotnetPack_ProducesWeatherModuleDllForAllTargetFrameworks()
    {
        var repoRoot = FindRepoRoot();
        var moduleProject = Path.Combine(repoRoot, "src", "UtilityAi.Compass.WeatherModule", "UtilityAi.Compass.WeatherModule.csproj");
        var projectDocument = XDocument.Load(moduleProject);
        var targetFrameworks = projectDocument
            .Descendants("TargetFrameworks")
            .First()
            .Value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var outputDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"pack \"{moduleProject}\" -c Debug -o \"{outputDir}\"",
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
                    .GetFiles(outputDir, "UtilityAi.Compass.WeatherModule.*.nupkg")
                    .FirstOrDefault(path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase));
                Assert.False(string.IsNullOrWhiteSpace(nupkgPath), "Expected weather module .nupkg output.");
                using var archive = ZipFile.OpenRead(nupkgPath);

                Assert.All(targetFrameworks, framework =>
                    Assert.NotNull(archive.GetEntry($"lib/{framework}/UtilityAi.Compass.WeatherModule.dll")));
            }
        }
        finally
        {
            try
            {
                Directory.Delete(outputDir, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
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
