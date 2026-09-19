# Testing strategy

## Layers

| Layer | Coverage |
| --- | --- |
| Example tests | Known A* cost, blocked line of sight, cover damage, cooldowns, winner state. |
| Property-style tests | 100 seeded maps preserve walkable cardinal routes; line of sight is symmetric. |
| Determinism tests | Identical seeds, AI decisions, matches, saves, and replays produce identical hashes. |
| Negative tests | Wrong team, occupied movement, invalid versions, rejected commands, replay tampering. |
| Migration tests | Legacy state-only save converts to the current domain state. |
| Simulation tests | Outcome accounting, action accounting, command caps, draw-regression guard. |
| Performance guards | 500 large-map A* queries and 100 bot matches stay inside generous CI budgets. |
| Contract build | Unity adapters compile against the intended narrow engine API surface. |
| Repository policy | Headers, documentation, secrets, prohibited references, and immutable action pins. |

## Why a dependency-free harness

The test executable uses a small in-repository runner because the production solution has no third-party NuGet dependency. It runs the same on both target operating systems, prints one result per case, writes structured JSON evidence, and returns nonzero on any failure. This is a deliberate supply-chain and reproducibility trade-off, not an attempt to replace mature frameworks for larger teams.

## Complete gate

`node scripts/verify.mjs` performs, in order:

1. locked offline restore;
2. formatting verification;
3. Release build with warnings as errors;
4. all 32 tests and a JSON report;
5. a 500-match deterministic tournament;
6. byte-for-byte comparison with checked-in JSON, CSV, and replay evidence;
7. replay validation;
8. authored scenario validation;
9. generated code-index freshness;
10. repository policy validation.

CI runs this gate on Ubuntu and Windows. CodeQL analyzes C# separately.

## Unity-specific scope

The repository continuously compile-checks the Unity adapter, but the normal CI gate does not start the Unity Editor. Edit Mode, Play Mode, scene loading, rendering, input, and platform builds require an installed licensed Unity 6000.3 editor. The contract build is intentionally labeled so it is not confused with those tests.

## Adding a test

Create a static test method, add it to the ordered registry in `tests/TacticalAI.Tests/Program.cs`, and ensure the test uses a fixed seed and isolated state. Update the count in documentation only after the complete gate passes.
