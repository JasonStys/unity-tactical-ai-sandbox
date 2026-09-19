# Validation report

Date: 2026-09-18

## Local result

| Gate | Result |
| --- | --- |
| Locked dependency-free restore | Pass |
| .NET Standard 2.1 portable-core build | Pass |
| .NET 10 Release build | Pass, 0 warnings and 0 errors |
| Unity adapter contract build | Pass |
| Test suite | Pass, 32 of 32 |
| Deterministic 500-match evidence comparison | Pass |
| Replay validation | Pass |
| Authored scenario validation | Pass |
| Code-index freshness | Pass |
| Repository/security policy | Pass |

The full local gate is `node scripts/verify.mjs`. Machine-readable test output is created at `artifacts/test-results.json` and is uploaded by CI.

## Remote result

Validated source commit: `88e67eb498712e20f74abf46778e4a32d59b472f`.

| GitHub check | Result | Evidence |
| --- | --- | --- |
| Ubuntu complete gate | Pass | [CI run 35415178898](https://github.com/JasonStys/unity-tactical-ai-sandbox/actions/runs/35415178898) |
| Windows complete gate | Pass | [CI run 35415178898](https://github.com/JasonStys/unity-tactical-ai-sandbox/actions/runs/35415178898) |
| Non-root container build and demo | Pass | [CI run 35415178898](https://github.com/JasonStys/unity-tactical-ai-sandbox/actions/runs/35415178898) |
| CodeQL C# analysis | Pass | [CodeQL run 35415178933](https://github.com/JasonStys/unity-tactical-ai-sandbox/actions/runs/35415178933) |
| Dependabot GitHub Actions scan | Pass | [update run 35414752727](https://github.com/JasonStys/unity-tactical-ai-sandbox/actions/runs/35414752727) |
| Dependabot Docker scan | Pass | [update run 35414752652](https://github.com/JasonStys/unity-tactical-ai-sandbox/actions/runs/35414752652) |

Both operating systems rebuilt the solution, ran all 32 tests, regenerated the 500-match evidence byte-for-byte, validated the replay and authored scenario, and uploaded machine-readable artifacts. The container job built the image, confirmed a non-root runtime identity, and ran the deterministic demo.

## Interpretation

A passing contract build proves the Unity-facing scripts compile against the narrow API surface represented by the stubs. It does not claim that the Unity Editor, scene, rendering, or platform player was executed. That remaining boundary is called out in `docs/LIMITATIONS.md`.
