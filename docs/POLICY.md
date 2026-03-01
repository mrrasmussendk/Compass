# Policy

Compass supports additive policy commands:

- `compass policy validate <policyFile>`
- `compass policy explain <request>`

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

## Default behavior

`policy explain` follows EnterpriseSafe defaults:

- Read-only style requests are allowed.
- Write/destructive style requests require approval.
