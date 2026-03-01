# Security

Compass Enterprise defaults to deny-by-default module installation protections:

- Required plugin manifest: `compass-manifest.json`
- Unsigned assemblies are blocked by default
- Override for local development only: `--allow-unsigned`

## Inspecting modules

Use:

- `compass inspect-module <path|package@version>`
- `compass inspect-module <path|package@version> --json`

Inspection reports include:

- UtilityAI module detection
- Signing status
- Manifest presence
- Declared capabilities and permissions
- Findings summary

## Threat model highlights

- Untrusted plugin binaries are treated as high risk.
- Missing manifests are blocked at install-time.
- Unsigned binaries are blocked unless explicitly overridden.
