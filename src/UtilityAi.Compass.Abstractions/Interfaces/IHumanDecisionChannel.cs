namespace UtilityAi.Compass.Abstractions.Interfaces;

public interface IHumanDecisionChannel
{
    Task SendRequestAsync(string requestId, string description, CancellationToken ct = default);
    Task<bool?> TryReceiveDecisionAsync(string requestId, CancellationToken ct = default);
}
