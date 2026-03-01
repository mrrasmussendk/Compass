# Operations

## Audit

- `compass audit list`
- `compass audit show <id> [--json]`

Audit data is read from `COMPASS_MEMORY_CONNECTION_STRING` when configured for SQLite.

## Replay

- `compass replay <id> [--no-exec]`

Replay is selection-focused and defaults to no side effects in this build.

## Doctor

- `compass doctor [--json]`

Doctor reports operational posture findings such as:

- Missing durable audit configuration
- Missing secret provider configuration
- Installed modules that should be inspected/sign-validated
