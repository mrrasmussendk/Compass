# Security

Compass Enterprise defaults to deny-by-default module installation protections:

- Required plugin manifest: `compass-manifest.json`
- Unsigned assemblies are blocked by default
- Override for local development only: `--allow-unsigned`

## Manifest schema (minimum accepted fields)

`compass-manifest.json` must include:

- `publisher` (string)
- `version` (string)
- `capabilities` (non-empty string array)
- `permissions` (string array)
- `sideEffectLevel` (string)

Additional optional fields currently parsed:

- `integrityHash`
- `networkEgressDomains`
- `fileAccessScopes`
- `requiredSecrets`

## Inspecting modules

Use:

- `compass inspect-module <path|package@version>`
- `compass inspect-module <path|package@version> --json`
- `compass --inspect-module <path|package@version> --json` (alias form)

Inspection reports include:

- UtilityAI module detection
- Signing status
- Manifest presence
- Declared capabilities and permissions
- Findings summary

## Install behavior by default

- `.dll` install:
  - must contain a UtilityAI module type
  - manifest must exist next to the dll
  - any `requiredSecrets` must already be set or entered at install prompt
  - must be signed unless `--allow-unsigned` is passed
- `.nupkg` install:
  - must contain compatible lib/runtimes assemblies
  - package root must contain `compass-manifest.json`
  - any `requiredSecrets` must already be set or entered at install prompt
  - assemblies must be signed unless `--allow-unsigned` is passed

Secrets entered at install prompt are only set for the current Compass process.

## Threat model highlights

- Untrusted plugin binaries are treated as high risk.
- Missing manifests are blocked at install-time.
- Unsigned binaries are blocked unless explicitly overridden.
