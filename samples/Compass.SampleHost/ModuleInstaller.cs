using System.Diagnostics;
using System.IO.Compression;

namespace Compass.SampleHost;

public static class ModuleInstaller
{
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
        process?.WaitForExit();
        return process?.ExitCode == 0;
    }

    public static async Task<string> InstallAsync(string moduleSpec, string pluginsPath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(pluginsPath);

        if (File.Exists(moduleSpec))
            return InstallFromFile(moduleSpec, pluginsPath);

        if (!TryParsePackageReference(moduleSpec, out var packageId, out var packageVersion))
            return "Module install failed: provide a .dll/.nupkg path or NuGet reference in the form PackageId@Version.";

        var downloadUrl = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/{packageVersion.ToLowerInvariant()}/{packageId.ToLowerInvariant()}.{packageVersion.ToLowerInvariant()}.nupkg";
        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(downloadUrl, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return $"Module install failed: could not download '{packageId}@{packageVersion}' (HTTP {(int)response.StatusCode}).";

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.nupkg");
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

    private static string InstallFromFile(string filePath, string pluginsPath)
    {
        var extension = Path.GetExtension(filePath);
        if (extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
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
        var copied = 0;

        foreach (var entry in archive.Entries.Where(IsPluginAssemblyEntry))
        {
            var destination = Path.Combine(pluginsPath, entry.Name);
            using var source = entry.Open();
            using var target = File.Create(destination);
            source.CopyTo(target);
            copied++;
        }

        if (copied == 0)
            return $"Module install failed: package '{packageId ?? Path.GetFileName(nupkgPath)}' does not contain plugin assemblies.";

        return $"Installed {copied} module assembly file(s) from '{packageId ?? Path.GetFileName(nupkgPath)}'.";
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
}
