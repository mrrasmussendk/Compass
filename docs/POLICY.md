# Policy

Vitruvian supports additive policy commands:

- `Vitruvian policy validate <policyFile>`
- `Vitruvian policy explain <request>`
- `Vitruvian --policy validate <policyFile>` (alias form)
- `Vitruvian --policy explain <request>` (alias form)

## Validation schema (current minimum)

Policy files are JSON and must contain a top-level `rules` array.

```json
{
  "name": "EnterpriseSafe",
  "rules": [
    { "id": "readonly-allow" }
  ]
}
```

## Validation outcomes

- **Success**: policy contains a top-level JSON `rules` array.
- **Failure**:
  - file missing
  - malformed JSON
  - missing `rules` array

## Default behavior

`policy explain` follows EnterpriseSafe defaults:

- Read-only style requests are allowed.
- Write/destructive style requests require approval.

Requests containing `write`, `update`, or `delete` are treated as approval-required signals by default.

## Example

```bash
Vitruvian policy explain "delete build artifacts under /tmp"
```

Expected output:

```text
Policy explain: matched EnterpriseSafe write/destructive guard; approval required.
```
