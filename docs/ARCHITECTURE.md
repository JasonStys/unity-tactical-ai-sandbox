# Architecture

## Goals

The design optimizes for deterministic behavior, engine independence, bounded work, explainable AI, and automated evidence. The portable core contains no file, network, Unity, or wall-clock dependencies.

## Components

| Component | Target | Responsibility |
| --- | --- | --- |
| `TacticalAI.Core` | .NET 10 and .NET Standard 2.1 | Domain state, algorithms, rules, AI, replay hashing, simulation. |
| `TacticalAI.Serialization` | .NET 10 | Strict JSON/save/replay mapping and CSV telemetry. |
| `TacticalAI.Headless` | .NET 10 | CLI orchestration and filesystem I/O. |
| Unity runtime adapter | Unity 6.3 LTS | Inspector authoring and presentation-facing calls. |
| Unity contract project | .NET 10 | Compile-checks linked adapter source against narrow API stubs. |
| Tests | .NET 10 | Deterministic functional, property, tamper, and budget checks. |

## Dependency rule

```text
Unity adapter ─┐
Headless CLI ──┼──> Core
Tests ─────────┘      ↑
  └──────────────> Serialization
Headless CLI ────> Serialization
```

The core cannot reference higher layers. Serialization translates through explicit document models instead of decorating domain objects. Unity scripts delegate to the core and do not duplicate tactical rules.

## State and command lifecycle

1. A player, bot, or replay produces an `ActionCommand`.
2. `TacticalEngine.Apply` resolves the actor and validates the entire command.
3. A successful command mutates bounded domain state and emits `ActionEvent` values.
4. The engine advances the active team and ticks incoming-team cooldowns.
5. Replay recording fingerprints the resulting canonical state.
6. Telemetry consumes events and terminal state without changing rules.

Rejected commands do not advance the turn. This atomicity is tested directly.

## Determinism contract

- Coordinates, identifiers, dictionaries, candidates, and sources use explicit stable ordering.
- Utility scores use integers; no floating-point values influence decisions.
- SplitMix64 defines a fixed random stream independent of platform runtime.
- State hashes serialize values in a canonical order with culture-independent formatting.
- Replay validation checks every intermediate hash, not only the final result.
- Batch evidence is compared byte-for-byte on Linux and Windows.

## Pathfinding design

A* uses Manhattan distance because movement is cardinal and every move cost is at least one. The heuristic never overestimates, so it remains admissible. The custom binary heap provides `O(log n)` insertion/removal. Duplicate open entries are permitted; stale entries are discarded using the best-cost table, which avoids the complexity and bookkeeping risk of decrease-key.

## Failure containment

Map dimensions, batch sizes, test durations, path expansions, utility evaluations, and turn counts all have explicit bounds. External JSON is parsed with bounded depth and validated before constructing domain state. Filesystem access stays in the headless boundary.
