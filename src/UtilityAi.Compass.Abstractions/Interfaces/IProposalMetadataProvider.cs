using UtilityAi.Consideration;
using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Abstractions.Interfaces;

/// <summary>
/// Provides <see cref="ProposalMetadata"/> for a given proposal at evaluation time.
/// Typically implemented by the plugin SDK to read metadata from attributes.
/// </summary>
public interface IProposalMetadataProvider
{
    /// <summary>Returns the metadata for the specified <paramref name="proposal"/>, or <c>null</c> if none is available.</summary>
    ProposalMetadata? GetMetadata(Proposal proposal, Runtime rt);
}
