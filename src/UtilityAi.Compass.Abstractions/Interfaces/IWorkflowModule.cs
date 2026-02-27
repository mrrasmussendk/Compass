using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Workflow;
using UtilityAi.Consideration;

namespace UtilityAi.Compass.Abstractions.Interfaces;

/// <summary>
/// A plugin module that provides a multi-step workflow.
/// Workflow modules propose starting new workflows, continuing active ones,
/// and repairing failed steps.
/// </summary>
public interface IWorkflowModule
{
    /// <summary>Returns the static definition describing the workflow and its steps.</summary>
    WorkflowDefinition Define();

    /// <summary>
    /// Proposes starting this workflow when no active workflow exists (or the current one is interruptible).
    /// </summary>
    /// <param name="rt">The current utility AI runtime context.</param>
    IEnumerable<Proposal> ProposeStart(UtilityAi.Utils.Runtime rt);

    /// <summary>
    /// Proposes the next step to execute within the active workflow.
    /// </summary>
    /// <param name="rt">The current utility AI runtime context.</param>
    /// <param name="active">The currently active workflow state.</param>
    IEnumerable<Proposal> ProposeSteps(UtilityAi.Utils.Runtime rt, ActiveWorkflow active);

    /// <summary>
    /// Proposes a repair action when a step or validation has failed.
    /// </summary>
    /// <param name="rt">The current utility AI runtime context.</param>
    /// <param name="active">The currently active workflow state.</param>
    /// <param name="directive">The repair directive describing what went wrong.</param>
    IEnumerable<Proposal> ProposeRepair(UtilityAi.Utils.Runtime rt, ActiveWorkflow active, RepairDirective directive);
}
