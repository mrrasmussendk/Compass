using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using UtilityAi.Capabilities;

namespace Compass.SampleHost;

public static class ModuleInstaller
{
    private static readonly HttpClient _httpClient = new();

    public static bool TryRunInstallScript()
    {
        var scriptPath = OperatingSystem.IsWindows()
            ? Path.Combine(AppContext.BaseDirectory, "scripts", "install.ps1")
            : Path.Combine(AppContext.BaseDirectory, "scripts", "install.sh");

        if (!File.Exists(scriptPath))
            return false;

        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("powershell", $"-ExecutionPolicy Bypass -File \"{scriptPath}\"")
            : new ProcessStartInfo("bash", $"\"{scriptPath}\"");

        startInfo.UseShellExecute = false;
        var process = Process.Start(startInfo);
        if (process is null)
            return false;

        process.WaitForExit();
        return process.ExitCode == 0;
    }

    public static async Task<string> InstallAsync(string moduleSpec, string pluginsPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(pluginsPath);

        if (File.Exists(moduleSpec))
            return InstallFromFile(moduleSpec, pluginsPath);

        if (!TryParsePackageReference(moduleSpec, out var packageId, out var packageVersion))
            return "Module install failed: provide a .dll/.nupkg path or NuGet reference in the form PackageId@Version.";

        var downloadUrl = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/{packageVersion.ToLowerInvariant()}/{packageId.ToLowerInvariant()}.{packageVersion.ToLowerInvariant()}.nupkg";
        using var response = await _httpClient.GetAsync(downloadUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return $"Module install failed: could not download '{packageId}@{packageVersion}' (HTTP {(int)response.StatusCode}).";

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid().ToString("N")}.nupkg");
        await using (var tempFile = File.Create(tempPath))
            await response.Content.CopyToAsync(tempFile, cancellationToken);

        try
        {
            return InstallFromNupkg(tempPath, pluginsPath, packageId);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public static bool TryParsePackageReference(string moduleSpec, out string packageId, out string packageVersion)
    {
        packageId = string.Empty;
        packageVersion = string.Empty;

        if (string.IsNullOrWhiteSpace(moduleSpec))
            return false;

        var parts = moduleSpec.Split('@', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        packageId = parts[0];
        packageVersion = parts[1];
        return packageId.Length > 0 && packageVersion.Length > 0;
    }

    public static bool TryParseInstallCommand(string input, out string moduleSpec)
    {
        moduleSpec = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        const string command = "/install-module";
        if (!input.TrimStart().StartsWith(command, StringComparison.OrdinalIgnoreCase))
            return false;

        moduleSpec = input.TrimStart()[command.Length..].Trim();
        return moduleSpec.Length > 0;
    }

    public static IReadOnlyList<string> ListInstalledModules(string pluginsPath)
    {
        if (!Directory.Exists(pluginsPath))
            return [];

        return Directory
            .EnumerateFiles(pluginsPath, "*.dll", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .OrderBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string InstallFromFile(string filePath, string pluginsPath)
    {
        var extension = Path.GetExtension(filePath);
        if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryValidateModuleAssembly(filePath, out var validationError))
                return validationError;

            var destination = Path.Combine(pluginsPath, Path.GetFileName(filePath));
            File.Copy(filePath, destination, overwrite: true);
            return $"Installed module DLL: {Path.GetFileName(filePath)}";
        }

        if (extension.Equals(".nupkg", StringComparison.OrdinalIgnoreCase))
            return InstallFromNupkg(filePath, pluginsPath);

        return "Module install failed: only .dll and .nupkg files are supported.";
    }

    private static string InstallFromNupkg(string nupkgPath, string pluginsPath, string? packageId = null)
    {
        using var archive = ZipFile.OpenRead(nupkgPath);
        var copiedFiles = new List<string>();
        var hasUtilityAiModule = false;

        foreach (var entry in archive.Entries.Where(IsPluginAssemblyEntry))
        {
            if (!IsCompatibleTargetFramework(entry.FullName))
                continue;

            var fileName = Path.GetFileName(entry.Name);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            var destination = Path.Combine(pluginsPath, fileName);
            using (var source = entry.Open())
            using (var target = File.Create(destination))
            {
                source.CopyTo(target);
            }
            copiedFiles.Add(destination);
            if (!hasUtilityAiModule && TryValidateModuleAssembly(destination, out _))
                hasUtilityAiModule = true;
        }

        if (copiedFiles.Count == 0)
            return $"Module install failed: package '{packageId ?? Path.GetFileName(nupkgPath)}' does not contain compatible .dll files in lib/ or runtimes/*/lib/.";

        if (!hasUtilityAiModule)
        {
            foreach (var file in copiedFiles.Where(File.Exists))
                File.Delete(file);

            return $"Module install failed: package '{packageId ?? Path.GetFileName(nupkgPath)}' does not contain a compatible UtilityAI module assembly.";
        }

        return $"Installed {copiedFiles.Count} module assembly file(s) from '{packageId ?? Path.GetFileName(nupkgPath)}'.";
    }

    private static bool IsPluginAssemblyEntry(ZipArchiveEntry entry)
    {
        if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return false;

        var normalized = entry.FullName.Replace('\\', '/');
        return normalized.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
            || (normalized.StartsWith("runtimes/", StringComparison.OrdinalIgnoreCase)
                && normalized.Contains("/lib/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryValidateModuleAssembly(string assemblyPath, out string error)
    {
        var loadContext = new AssemblyLoadContext($"Compass.ModuleValidation.{Guid.NewGuid():N}", isCollectible: true);
        try
        {
            // Load from a memory stream to avoid holding a file lock on Windows.
            var bytes = File.ReadAllBytes(assemblyPath);
            using var ms = new MemoryStream(bytes);
            var assembly = loadContext.LoadFromStream(ms);
            var hasModule = assembly.GetExportedTypes().Any(IsUtilityAiModuleType);
            if (hasModule)
            {
                error = string.Empty;
                return true;
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            if (ex.Types.OfType<Type>().Any(IsUtilityAiModuleType))
            {
                error = string.Empty;
                return true;
            }
        }
        catch (FileNotFoundException)
        {
            // handled below
        }
        catch (FileLoadException)
        {
            // handled below
        }
        catch (BadImageFormatException)
        {
            // handled below
        }
        catch (NotSupportedException)
        {
            // handled below
        }
        finally
        {
            loadContext.Unload();
        }

        error = $"Module install failed: '{Path.GetFileName(assemblyPath)}' is not a compatible UtilityAI module assembly.";
        return false;
    }

    private static bool IsCompatibleTargetFramework(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var tfm = parts.Length >= 3 && parts[0].Equals("lib", StringComparison.OrdinalIgnoreCase)
            ? parts[1]
            : parts.Length >= 5
                && parts[0].Equals("runtimes", StringComparison.OrdinalIgnoreCase)
                && parts[2].Equals("lib", StringComparison.OrdinalIgnoreCase)
                ? parts[3]
                : null;

        if (string.IsNullOrWhiteSpace(tfm))
            return false;

        if (tfm.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!tfm.StartsWith("net", StringComparison.OrdinalIgnoreCase))
            return false;

        var tfmBody = tfm[3..];
        var versionText = new string(tfmBody.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        if (versionText.Length == 0 || !Version.TryParse(versionText, out var version))
            return false;

        if (tfmBody.StartsWith($"{versionText}-", StringComparison.OrdinalIgnoreCase)
            && !IsCompatiblePlatformTfm(tfmBody[(versionText.Length + 1)..]))
            return false;

        return version.Major <= Environment.Version.Major;
    }

    private static bool IsCompatiblePlatformTfm(string platformPart)
    {
        if (platformPart.StartsWith("windows", StringComparison.OrdinalIgnoreCase))
            return OperatingSystem.IsWindows();
        if (platformPart.StartsWith("linux", StringComparison.OrdinalIgnoreCase))
            return OperatingSystem.IsLinux();
        if (platformPart.StartsWith("osx", StringComparison.OrdinalIgnoreCase))
            return OperatingSystem.IsMacOS();

        return true;
    }

    private static bool IsUtilityAiModuleType(Type type)
    {
        if (!type.IsClass || type.IsAbstract)
            return false;

        return type.GetInterfaces()
            .Any(i => i.FullName == typeof(ICapabilityModule).FullName);
    }
}
