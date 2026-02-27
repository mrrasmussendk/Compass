using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Compass.Abstractions.Workflow;
using UtilityAi.Compass.StandardModules;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Tests;

public class FileReadWorkflowTests
{
    [Fact]
    public void Define_ReturnsCorrectWorkflowDefinition()
    {
        var wf = new FileReadWorkflow();
        var def = wf.Define();

        Assert.Equal("file-read", def.WorkflowId);
        Assert.Equal("Read File", def.DisplayName);
        Assert.Contains(GoalTag.Answer, def.Goals);
        Assert.Contains(Lane.Execute, def.Lanes);
        Assert.Single(def.Steps);
        Assert.Equal("read", def.Steps[0].StepId);
        Assert.True(def.CanInterrupt);
        Assert.Equal(0.1, def.EstimatedCost);
        Assert.Equal(0.0, def.RiskLevel);
    }

    [Fact]
    public void ProposeStart_ReturnsProposal_WhenUserRequestExists()
    {
        var wf = new FileReadWorkflow();
        var bus = new EventBus();
        bus.Publish(new UserRequest("read file notes.txt"));
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = wf.ProposeStart(rt).ToList();

        Assert.Single(proposals);
        Assert.Equal("file-read.read", proposals[0].Id);
    }

    [Fact]
    public void ProposeStart_ReturnsEmpty_WhenNoUserRequest()
    {
        var wf = new FileReadWorkflow();
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);

        var proposals = wf.ProposeStart(rt).ToList();

        Assert.Empty(proposals);
    }

    [Fact]
    public async Task ProposeStart_ActReadsFile_WhenFileExists()
    {
        var wf = new FileReadWorkflow();
        var bus = new EventBus();
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "workflow file contents");

        try
        {
            bus.Publish(new UserRequest($"read file {tempFile}"));
            var rt = new UtilityAi.Utils.Runtime(bus, 0);

            var proposals = wf.ProposeStart(rt).ToList();
            Assert.Single(proposals);

            await proposals[0].Act(CancellationToken.None);

            var response = bus.GetOrDefault<AiResponse>();
            Assert.NotNull(response);
            Assert.Equal("workflow file contents", response.Text);

            var stepResult = bus.GetOrDefault<StepResult>();
            Assert.NotNull(stepResult);
            Assert.Equal(StepOutcome.Succeeded, stepResult.Outcome);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ProposeSteps_ReturnsEmpty()
    {
        var wf = new FileReadWorkflow();
        var bus = new EventBus();
        var rt = new UtilityAi.Utils.Runtime(bus, 0);
        var active = new ActiveWorkflow("file-read", "run-1", "read", WorkflowStatus.Active);

        Assert.Empty(wf.ProposeSteps(rt, active));
    }
}
