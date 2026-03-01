using UtilityAi.Capabilities;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Consideration;

namespace UtilityAi.Compass.Runtime.Modules;

/// <summary>
/// Capability module that orchestrates workflow modules by calling their ProposeStart/ProposeSteps/ProposeRepair methods.
/// </summary>
public sealed class WorkflowOrchestratorModule : ICapabilityModule
{
    private readonly IEnumerable<IWorkflowModule> _workflowModules;

    public WorkflowOrchestratorModule(IEnumerable<IWorkflowModule> workflowModules)
    {
        _workflowModules = workflowModules;
    }

    /// <inheritdoc />
    public IEnumerable<Proposal> Propose(UtilityAi.Utils.Runtime rt)
    {
        var activeWorkflow = rt.Bus.GetOrDefault<ActiveWorkflow>();
        var repairDirective = rt.Bus.GetOrDefault<RepairDirective>();

        foreach (var workflow in _workflowModules)
        {
            if (repairDirective is not null && activeWorkflow is not null)
            {
                // Repair mode
                foreach (var proposal in workflow.ProposeRepair(rt, activeWorkflow, repairDirective))
                    yield return proposal;
            }
            else if (activeWorkflow is not null)
            {
                // Continue existing workflow
                foreach (var proposal in workflow.ProposeSteps(rt, activeWorkflow))
                    yield return proposal;
            }
            else
            {
                // Start new workflow
                foreach (var proposal in workflow.ProposeStart(rt))
                    yield return proposal;
            }
        }
    }
}
