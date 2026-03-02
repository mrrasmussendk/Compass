# Compound Request Handling

Compass supports **compound requests** — user messages that contain multiple independent intents in a single sentence.

## Example

```
User: "Make a file called u.txt insert the word gold. Then give me the colors of the rainbow"
```

This contains two independent intents:
1. **File creation** — create `u.txt` with content "gold"
2. **Question answering** — list the colors of the rainbow

## How It Works

Compound request handling is **module-agnostic** and happens at the host orchestration level. No individual module needs to understand compound requests — each module only handles its own domain.

### Pipeline

```
┌──────────────────┐
│  User Request     │  "create file u.txt then send SMS with rainbow colors"
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  IsCompoundRequest│  Heuristic detection (sequential indicators + multi-verb)
│  (static check)   │
└────────┬─────────┘
         │  compound detected
         ▼
┌──────────────────┐
│  DecomposeRequest │  LLM splits into independent sub-tasks (JSON array)
│  (LLM call)       │  → ["create file u.txt with gold", "send SMS with rainbow colors"]
└────────┬─────────┘
         │
         ▼  for each sub-task:
┌──────────────────┐
│  Full Pipeline    │  GoalRouterSensor → LaneRouter → Governance → Module
│  (per sub-task)   │  Sub-task 1 → FileCreationModule
│                   │  Sub-task 2 → SmsModule (if installed)
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│  Combined Response│  All sub-task responses joined into single output
└──────────────────┘
```

### Detection Heuristics

`CompoundRequestOrchestrator.IsCompoundRequest()` checks for:

1. **Sequential indicators**: ` then `, ` and then `, ` afterwards `, ` after that `, ` next `, ` followed by `, ` after `
2. **Multiple action verbs** (≥ 2): `create`, `write`, `read`, `delete`, `update`, `insert`, `add`, `remove`, `modify`, `input`

### LLM Decomposition

When a compound request is detected, `CompoundRequestOrchestrator.DecomposeRequestAsync()` calls the LLM with:

```
System: Split this compound user request into independent sub-tasks.
        Return a JSON array of strings, one per task.
        Keep each sub-task self-contained (include relevant context like filenames).
        If not compound, return the original as a single-element array.
        Only return valid JSON, nothing else.

User: <original request>
```

The LLM returns a JSON array:
```json
["Create a file called u.txt with the word gold", "Give me the colors of the rainbow"]
```

### Per-Sub-Task Execution

Each sub-task runs through the **complete** Compass pipeline independently:

1. **Sensors** classify the sub-task's intent (GoalTag) and lane
2. **Modules** propose actions based on the classified intent
3. **Governance** filters and scores proposals (conflicts, cooldowns, cost/risk)
4. The **winning proposal** is executed

This means **any installed module** — built-in or plugin — automatically participates in compound request handling without modification.

## Module-Agnostic Design

This is the key architectural decision: compound handling lives at the **host orchestration level**, not inside any module.

### Why not a CompoundRequestModule?

A module that tries to handle compound requests internally would need to understand every possible capability:

```
❌ CompoundRequestModule:
   - Knows about file creation → hardcoded logic
   - Knows about SMS → needs extending
   - Knows about weather → needs extending
   - Knows about email → needs extending
   - Every new plugin requires changes here
```

Instead, the host splits the compound request and lets each module handle its own domain:

```
✅ Host-level orchestration:
   - Split into sub-tasks (module-agnostic)
   - Sub-task "create file" → FileCreationModule handles it
   - Sub-task "send SMS"   → SmsModule handles it (if installed)
   - Sub-task "check weather" → WeatherModule handles it (if installed)
   - New plugins work automatically, zero changes needed
```

## Fallback Behavior

| Condition | Behavior |
|-----------|----------|
| Model client available | LLM decomposes → per-sub-task pipeline execution |
| No model client | `CompoundRequestModule` publishes guidance message |
| LLM returns invalid JSON | Falls back to single-request processing |
| LLM returns empty array | Falls back to single-request processing |
| Cancellation requested | `OperationCanceledException` propagated |

## Concurrency & Thread Safety

- `IsCompoundRequest()` is a **pure static method** — safe to call from any thread
- `DecomposeRequestAsync()` is **stateless** — concurrent calls don't interfere
- Each sub-task gets its own `EventBus` instance in `RunSingleRequestAsync()`
- The `EventBus` itself uses `lock`-based synchronization for thread-safe publish/subscribe
- Cancellation tokens are properly propagated through the entire decomposition + execution chain

## Testing

Tests are in `CompoundRequestOrchestratorTests.cs` and `CompoundRequestModuleTests.cs`, covering:

| Category | Tests |
|----------|-------|
| Detection heuristics | Sequential indicators, multi-verb, case insensitivity, boundary cases, null/empty |
| LLM decomposition | Valid JSON, invalid JSON, empty arrays, null elements, whitespace filtering |
| Cancellation | Pre-cancelled tokens, mid-flight cancellation |
| Race conditions | 20 concurrent decompositions, mixed cancel/succeed, concurrent reads, slow vs fast model |
| Error handling | Model exceptions, timeout exceptions |
| CompoundRequestModule fallback | No-compound, not-compound, guidance message, positive utility |

## Configuration

No additional configuration is required. Compound request handling is automatically enabled when a model client is configured. Without a model client, the `CompoundRequestModule` provides a fallback guidance message.
