using UtilityAi.Consideration;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Abstractions.Interfaces;

public interface IProposalMetadataProvider
{
    ProposalMetadata? GetMetadata(Proposal proposal, Runtime rt);
}
