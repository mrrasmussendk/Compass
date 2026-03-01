using UtilityAi.Compass.Abstractions.Facts;
using UtilityAi.Compass.Abstractions.Interfaces;
using UtilityAi.Consideration;
using UtilityAi.Utils;

namespace UtilityAi.Compass.Runtime.Modules;

/// <summary>
/// Composite metadata provider that tries multiple providers in sequence.
/// Returns the first non-null metadata found.
/// </summary>
public sealed class CompositeMetadataProvider : IProposalMetadataProvider
{
    private readonly IReadOnlyList<IProposalMetadataProvider> _providers;

    public CompositeMetadataProvider(IEnumerable<IProposalMetadataProvider> providers)
    {
        _providers = providers.ToList();
    }

    public ProposalMetadata? GetMetadata(Proposal proposal, UtilityAi.Utils.Runtime rt)
    {
        foreach (var provider in _providers)
        {
            var metadata = provider.GetMetadata(proposal, rt);
            if (metadata is not null)
                return metadata;
        }

        return null;
    }
}
