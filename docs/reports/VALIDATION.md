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

GitHub Actions run links will be recorded here after the repository's first successful remote validation.

## Interpretation

A passing contract build proves the Unity-facing scripts compile against the narrow API surface represented by the stubs. It does not claim that the Unity Editor, scene, rendering, or platform player was executed. That remaining boundary is called out in `docs/LIMITATIONS.md`.
