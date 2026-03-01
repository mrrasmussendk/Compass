namespace Compass.SampleHost;

/// <summary>
/// Loads environment variables from a <c>.env.compass</c> file so the host
/// "just works" after running <c>scripts/install.sh</c> or <c>scripts/install.ps1</c>.
/// Supports three line formats:
/// <list type="bullet">
///   <item><c>KEY=VALUE</c> (standard .env)</item>
///   <item><c>export KEY=VALUE</c> (bash / install.sh)</item>
///   <item><c>$env:KEY='VALUE'</c> (PowerShell / install.ps1)</item>
/// </list>
/// Existing environment variables are not overwritten by default, allowing callers
/// to override individual values via the shell when needed. If <c>COMPASS_PROFILE</c>
/// is set, an additional <c>.env.compass.&lt;profile&gt;</c> file is loaded after the base file.
/// </summary>
public static class EnvFileLoader
{
    private const string FileName = ".env.compass";
    private const string ProfileVariableName = "COMPASS_PROFILE";

    /// <summary>
    /// Searches for <c>.env.compass</c> starting from <paramref name="startDirectory"/>
    /// (defaults to <see cref="Directory.GetCurrentDirectory"/>) and walking up to
    /// ancestor directories. If the file is found, each recognised line is set as an
    /// environment variable for the current process.
    /// </summary>
    public static void Load(string? startDirectory = null, bool overwriteExisting = false)
    {
        var searchDirectories = new[]
        {
            startDirectory ?? Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        };
        var path = FindFile(searchDirectories);
        if (path is null)
            return;

        var keysLoadedFromBaseFile = new HashSet<string>(StringComparer.Ordinal);
        LoadFile(path, overwriteExisting, keysLoadedFromBaseFile);

        var profile = Environment.GetEnvironmentVariable(ProfileVariableName)?.Trim();
        if (string.IsNullOrWhiteSpace(profile))
            return;

        var profilePath = FindFile(searchDirectories, $"{FileName}.{profile}");
        if (profilePath is null || string.Equals(profilePath, path, StringComparison.OrdinalIgnoreCase))
            return;

        LoadFile(profilePath, overwriteExisting, keysLoadedFromBaseFile);
    }

    private static void LoadFile(string path, bool overwriteExisting, ISet<string> keysLoadedFromBaseFile)
    {
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var (key, value) = ParseLine(rawLine);
            if (key is null)
                continue;

            if (overwriteExisting || keysLoadedFromBaseFile.Contains(key) || Environment.GetEnvironmentVariable(key) is null)
            {
                Environment.SetEnvironmentVariable(key, value);
                keysLoadedFromBaseFile.Add(key);
            }
        }
    }

    public static string? FindFile(string startDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startDirectory);
        return FindFileInAncestors(startDirectory);
    }

    public static string? FindFile(IEnumerable<string?> startDirectories)
    {
        return FindFile(startDirectories, FileName);
    }

    private static string? FindFile(IEnumerable<string?> startDirectories, string fileName)
    {
        ArgumentNullException.ThrowIfNull(startDirectories);
        foreach (var startDirectory in startDirectories)
        {
            if (string.IsNullOrWhiteSpace(startDirectory))
                continue;

            var path = FindFileInAncestors(startDirectory, fileName);
            if (path is not null)
                return path;
        }

        return null;
    }

    private static string? FindFileInAncestors(string startDirectory)
        => FindFileInAncestors(startDirectory, FileName);

    private static string? FindFileInAncestors(string startDirectory, string fileName)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    public static (string? Key, string? Value) ParseLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            return (null, null);

        // PowerShell format: $env:KEY='VALUE'
        if (trimmed.StartsWith("$env:", StringComparison.OrdinalIgnoreCase))
        {
            var rest = trimmed["$env:".Length..];
            var eqIndex = rest.IndexOf('=');
            if (eqIndex <= 0)
                return (null, null);

            var key = rest[..eqIndex];
            var value = Unquote(rest[(eqIndex + 1)..]);
            return (key, value);
        }

        // Bash format: export KEY=VALUE
        if (trimmed.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed["export ".Length..].TrimStart();

        // Standard: KEY=VALUE
        var idx = trimmed.IndexOf('=');
        if (idx <= 0)
            return (null, null);

        return (trimmed[..idx], Unquote(trimmed[(idx + 1)..]));
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '\'' && value[^1] == '\'') ||
             (value[0] == '"' && value[^1] == '"')))
            return value[1..^1];

        return value;
    }
}
