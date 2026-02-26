namespace UtilityAi.Nexus.Abstractions.Facts;

public sealed record GoalSelected(GoalTag Goal, double Confidence, string? Reason = null);
public sealed record LaneSelected(Lane Lane);
public sealed record CorrelationId(string Value);
public sealed record UserRequest(string Text, string? UserId = null);
public sealed record AiResponse(string Text, string? CorrelationId = null);
