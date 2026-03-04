# Operations

## Audit

- `Vitruvian audit list`
- `Vitruvian audit show <id> [--json]`
- `Vitruvian --audit list` / `Vitruvian --audit show <id> [--json]` (alias form)

Audit data is read from `VITRUVIAN_MEMORY_CONNECTION_STRING` when configured for SQLite.

### Typical flow

1. Configure memory:
   - `VITRUVIAN_MEMORY_CONNECTION_STRING=Data Source=/path/to/Vitruvian.db;Pooling=False`
2. Run requests in Vitruvian.
3. List records with `Vitruvian audit list`.
4. Inspect one record with `Vitruvian audit show <id> --json`.

## Replay

- `Vitruvian replay <id> [--no-exec]`
- `Vitruvian --replay <id> [--no-exec]` (alias form)

Replay is selection-focused and defaults to no side effects in this build.

## Doctor

- `Vitruvian doctor [--json]`
- `Vitruvian --doctor [--json]` (alias form)

Doctor reports operational posture findings such as:

- Missing durable audit configuration
- Missing secret provider configuration
- Installed modules that should be inspected/sign-validated

### Recommended operator baseline

- Set `VITRUVIAN_MEMORY_CONNECTION_STRING`
- Set `VITRUVIAN_SECRET_PROVIDER`
- Run `Vitruvian doctor --json` in CI to fail or alert on insecure posture
