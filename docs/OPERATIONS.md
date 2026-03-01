# Operations

## Audit

- `compass audit list`
- `compass audit show <id> [--json]`
- `compass --audit list` / `compass --audit show <id> [--json]` (alias form)

Audit data is read from `COMPASS_MEMORY_CONNECTION_STRING` when configured for SQLite.

### Typical flow

1. Configure memory:
   - `COMPASS_MEMORY_CONNECTION_STRING=Data Source=/path/to/compass.db;Pooling=False`
2. Run requests in Compass.
3. List records with `compass audit list`.
4. Inspect one record with `compass audit show <id> --json`.

## Replay

- `compass replay <id> [--no-exec]`
- `compass --replay <id> [--no-exec]` (alias form)

Replay is selection-focused and defaults to no side effects in this build.

## Doctor

- `compass doctor [--json]`
- `compass --doctor [--json]` (alias form)

Doctor reports operational posture findings such as:

- Missing durable audit configuration
- Missing secret provider configuration
- Installed modules that should be inspected/sign-validated

### Recommended operator baseline

- Set `COMPASS_MEMORY_CONNECTION_STRING`
- Set `COMPASS_SECRET_PROVIDER`
- Run `compass doctor --json` in CI to fail or alert on insecure posture
