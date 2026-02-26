using UtilityAi.Consideration;
using UtilityAi.Nexus.Abstractions.Facts;
using UtilityAi.Utils;

namespace UtilityAi.Nexus.Abstractions.Interfaces;

public interface IProposalMetadataProvider
{
    ProposalMetadata? GetMetadata(Proposal proposal, Runtime rt);
}
