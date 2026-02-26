namespace UtilityAi.Compass.Abstractions.Interfaces;

/// <summary>
/// Communication channel for human-in-the-loop decision gates.
/// Implementations deliver approval requests to a human and poll for their response.
/// </summary>
public interface IHumanDecisionChannel
{
    /// <summary>Sends an approval request with the given <paramref name="requestId"/> and human-readable <paramref name="description"/>.</summary>
    Task SendRequestAsync(string requestId, string description, CancellationToken ct = default);

    /// <summary>Attempts to retrieve a human decision for the specified <paramref name="requestId"/>. Returns <c>true</c> for approved, <c>false</c> for rejected, or <c>null</c> if no decision has been made yet.</summary>
    Task<bool?> TryReceiveDecisionAsync(string requestId, CancellationToken ct = default);
}
