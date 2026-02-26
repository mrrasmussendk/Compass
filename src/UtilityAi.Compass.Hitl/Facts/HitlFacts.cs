namespace UtilityAi.Compass.Hitl.Facts;

/// <summary>Fact published when a proposal requires human approval.</summary>
/// <param name="RequestId">Unique identifier for this approval request.</param>
/// <param name="Description">Human-readable description of the operation requiring approval.</param>
/// <param name="ProposalId">The identifier of the proposal that triggered the request.</param>
public sealed record HitlRequest(string RequestId, string Description, string ProposalId);

/// <summary>Fact published when a human has made an approval or rejection decision.</summary>
/// <param name="RequestId">The identifier of the request being decided.</param>
/// <param name="Approved"><c>true</c> if approved; <c>false</c> if rejected.</param>
/// <param name="Reason">Optional reason provided by the human reviewer.</param>
public sealed record HitlDecision(string RequestId, bool Approved, string? Reason = null);

/// <summary>Fact indicating that a human-in-the-loop request is awaiting a decision.</summary>
/// <param name="RequestId">The identifier of the pending request.</param>
public sealed record HitlPending(string RequestId);

/// <summary>Fact published when a human-in-the-loop request has been approved.</summary>
/// <param name="RequestId">The identifier of the approved request.</param>
/// <param name="ProposalId">The identifier of the approved proposal.</param>
public sealed record HitlApproved(string RequestId, string ProposalId);

/// <summary>Fact published when a human-in-the-loop request has been rejected.</summary>
/// <param name="RequestId">The identifier of the rejected request.</param>
/// <param name="ProposalId">The identifier of the rejected proposal.</param>
/// <param name="Reason">Optional reason for rejection.</param>
public sealed record HitlRejected(string RequestId, string ProposalId, string? Reason = null);
