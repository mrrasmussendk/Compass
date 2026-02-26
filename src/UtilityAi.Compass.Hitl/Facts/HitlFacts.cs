namespace UtilityAi.Compass.Hitl.Facts;

public sealed record HitlRequest(string RequestId, string Description, string ProposalId);
public sealed record HitlDecision(string RequestId, bool Approved, string? Reason = null);
public sealed record HitlPending(string RequestId);
public sealed record HitlApproved(string RequestId, string ProposalId);
public sealed record HitlRejected(string RequestId, string ProposalId, string? Reason = null);
