using Compass.SampleHost;

namespace UtilityAi.Compass.Tests;

public class EnvFileLoaderTests
{
    [Theory]
    [InlineData("KEY=value", "KEY", "value")]
    [InlineData("export KEY=value", "KEY", "value")]
    [InlineData("export  KEY=value", "KEY", "value")]
    [InlineData("$env:KEY='value'", "KEY", "value")]
    [InlineData("$env:KEY=\"value\"", "KEY", "value")]
    [InlineData("KEY='quoted value'", "KEY", "quoted value")]
    [InlineData("KEY=\"double quoted\"", "KEY", "double quoted")]
    [InlineData("COMPASS_MODEL_PROVIDER=openai", "COMPASS_MODEL_PROVIDER", "openai")]
    public void ParseLine_RecognisesValidFormats(string line, string expectedKey, string expectedValue)
    {
        var (key, value) = EnvFileLoader.ParseLine(line);
        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("# comment")]
    [InlineData("no-equals-sign")]
    public void ParseLine_ReturnsNull_ForNonAssignmentLines(string line)
    {
        var (key, _) = EnvFileLoader.ParseLine(line);
        Assert.Null(key);
    }

    [Fact]
    public void FindFile_ReturnsNull_WhenFileDoesNotExist()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var result = EnvFileLoader.FindFile(dir);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FindFile_WalksUpToFindFile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var child = Path.Combine(root, "a", "b");
        Directory.CreateDirectory(child);
        var envFile = Path.Combine(root, ".env.compass");
        File.WriteAllText(envFile, "KEY=value");
        try
        {
            var result = EnvFileLoader.FindFile(child);
            Assert.NotNull(result);
            Assert.Equal(envFile, result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindFile_WithMultipleStartDirectories_ReturnsFirstMatch()
    {
        var missingRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var child = Path.Combine(root, "nested");
        Directory.CreateDirectory(missingRoot);
        Directory.CreateDirectory(child);
        var envFile = Path.Combine(root, ".env.compass");
        File.WriteAllText(envFile, "KEY=value");
        try
        {
            var result = EnvFileLoader.FindFile([missingRoot, child]);
            Assert.NotNull(result);
            Assert.Equal(envFile, result);
        }
        finally
        {
            Directory.Delete(missingRoot, recursive: true);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Load_SetsEnvironmentVariables_FromFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var envFile = Path.Combine(dir, ".env.compass");
        var uniqueKey = $"COMPASS_TEST_{Guid.NewGuid():N}";
        File.WriteAllText(envFile, $"{uniqueKey}=test-value");
        try
        {
            EnvFileLoader.Load(dir);
            Assert.Equal("test-value", Environment.GetEnvironmentVariable(uniqueKey));
        }
        finally
        {
            Environment.SetEnvironmentVariable(uniqueKey, null);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_DoesNotOverwriteExistingVariables()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var envFile = Path.Combine(dir, ".env.compass");
        var uniqueKey = $"COMPASS_TEST_{Guid.NewGuid():N}";
        File.WriteAllText(envFile, $"{uniqueKey}=file-value");
        Environment.SetEnvironmentVariable(uniqueKey, "existing-value");
        try
        {
            EnvFileLoader.Load(dir);
            Assert.Equal("existing-value", Environment.GetEnvironmentVariable(uniqueKey));
        }
        finally
        {
            Environment.SetEnvironmentVariable(uniqueKey, null);
            Directory.Delete(dir, recursive: true);
        }
    }
}
