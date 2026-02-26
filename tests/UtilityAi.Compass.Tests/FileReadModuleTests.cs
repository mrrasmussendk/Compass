using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.StandardModules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class FileReadModuleTests
{
    [Fact]
    public void Propose_ReturnsProposal_WhenUserRequestExists()
    {
        var module = new FileReadModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("read file notes.txt"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("file-read.read", proposals[0].Id);
        Assert.Equal("Read a file and return its contents", proposals[0].Description);
    }

    [Fact]
    public void Propose_ReturnsEmpty_WhenNoUserRequest()
    {
        var module = new FileReadModule();
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public async Task Propose_ActReadsFile_WhenFileExists()
    {
        var module = new FileReadModule();
        var bus = new EventBus();
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "file contents here");

        bus.Publish(new UserRequest($"read file {tempFile}"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);

        await proposals[0].Act(CancellationToken.None);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        Assert.Equal("file contents here", response.Text);

        File.Delete(tempFile);
    }

    [Fact]
    public async Task Propose_ActPublishesNotFound_WhenFileMissing()
    {
        var module = new FileReadModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("read file /nonexistent/path/missing.txt"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);

        await proposals[0].Act(CancellationToken.None);

        var response = bus.GetOrDefault<AiResponse>();
        Assert.NotNull(response);
        Assert.Contains("File not found", response.Text);
    }

    [Fact]
    public void ExtractFilePath_FindsPathWithExtension()
    {
        var path = FileReadModule.ExtractFilePath("read file notes.txt");
        Assert.Equal("notes.txt", path);
    }

    [Fact]
    public void ExtractFilePath_FindsPathWithDirectory()
    {
        var path = FileReadModule.ExtractFilePath("show /home/user/data.csv");
        Assert.Equal("/home/user/data.csv", path);
    }

    [Fact]
    public void Propose_HasPositiveUtility()
    {
        var module = new FileReadModule();
        var bus = new EventBus();
        bus.Publish(new UserRequest("read file test.txt"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = module.Propose(rt).ToList();
        Assert.Single(proposals);
        Assert.True(proposals[0].Utility(rt) > 0);
    }
}
