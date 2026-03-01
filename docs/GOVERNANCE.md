# Governance

Compass executes proposals through an explicit governance pipeline:

1. Proposal generation
2. Goal/lane and workflow filtering
3. Conflict and cooldown handling
4. Policy and trust checks (including module signature/manifest checks at install)
5. Cost/risk scoring with hysteresis
6. Execution and audit persistence

## Explainability

Each CLI execution path now surfaces a deterministic explanation source:

- `compass inspect-module` explains manifest/signing/capability findings
- `compass policy explain <request>` reports matching default policy behavior
- `compass audit list` / `compass audit show` expose persisted execution records

This preserves compatibility while adding additive governance entry points.
