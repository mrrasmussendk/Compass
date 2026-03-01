# Governance

Compass executes proposals through an explicit governance pipeline:

1. Proposal generation
2. Goal/lane and workflow filtering
3. Conflict and cooldown handling
4. Policy and trust checks (including module signature/manifest checks at install)
5. Cost/risk scoring with hysteresis
6. Execution and audit persistence

## Effective scoring model

The current governed selection strategy computes an effective score as:

`effectiveScore = utility - (costWeight * trustedCost) - (riskWeight * trustedRisk)`

Where `trustedCost` and `trustedRisk` are bounded by minimum values based on side-effect level:

- `ReadOnly`: minimum cost/risk of `0.0`
- `Write`: minimum cost `0.2`, minimum risk `0.35`
- `Destructive`: minimum cost `0.4`, minimum risk `0.7`

This prevents modules from under-reporting risk for high-impact actions.

## Hysteresis behavior

To reduce oscillation between near-equal proposals, the previous winner may be retained when:

- `lastWinnerScore + stickinessBonus >= bestScore - hysteresisEpsilon`

This keeps behavior stable under noisy utility differences.

## Explainability

Each CLI execution path now surfaces a deterministic explanation source:

- `compass inspect-module` explains manifest/signing/capability findings
- `compass policy explain <request>` reports matching default policy behavior
- `compass audit list` / `compass audit show` expose persisted execution records

This preserves compatibility while adding additive governance entry points.

## Plan/Review/Commit posture

Governance commands document approval posture even when action execution is disabled:

- Read-only requests map to allow paths.
- Write/destructive requests map to approval-required paths.
- Replay is selection-first and no-side-effect by default in this build.
