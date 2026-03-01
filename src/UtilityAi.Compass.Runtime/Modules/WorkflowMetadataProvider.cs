using System.Collections.Concurrent;
using UtilityAi.Compass.Abstractions;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Runtime.Modules;

/// <summary>
/// Metadata provider that extracts metadata from workflow definitions.
/// Maps proposal IDs to their originating workflow modules.
/// </summary>
public sealed class WorkflowMetadataProvider : IProposalMetadataProvider
{
    private readonly ConcurrentDictionary<string, IWorkflowModule> _workflowsByProposalPrefix = new();

    public WorkflowMetadataProvider(IEnumerable<IWorkflowModule> workflowModules)
    {
        foreach (var workflow in workflowModules)
        {
            var def = workflow.Define();
            // Map workflow ID to the module (proposals start with workflow ID)
            _workflowsByProposalPrefix[def.WorkflowId] = workflow;
        }
    }

    public ProposalMetadata? GetMetadata(Proposal proposal, UtilityAi.Utils.Runtime rt)
    {
        // Check if this proposal comes from a workflow module
        foreach (var kvp in _workflowsByProposalPrefix)
        {
            if (proposal.Id.StartsWith(kvp.Key))
            {
                var def = kvp.Value.Define();
                return new ProposalMetadata(
                    Domain: def.WorkflowId,
                    Lane: def.Lanes.FirstOrDefault(),
                    Goals: def.Goals.ToList(),
                    EstimatedCost: def.EstimatedCost,
                    RiskLevel: def.RiskLevel);
            }
        }

        return null;
    }
}
