# Research notes

Research was reviewed on 2026-09-18 and translated into project-specific decisions rather than copied as instructions.

## Platform choices

- Unity's official release material identifies Unity 6.3 as an LTS release with support through December 2027. The integration metadata pins 6000.3.13f1, an official 6.3 patch release.
- Microsoft documents .NET 10 as an LTS release and C# 14 as its language version. The headless tools pin SDK 10.0.401 through `global.json`.
- The engine-facing library also targets .NET Standard 2.1, separating portable gameplay logic from .NET 10-only file and JSON tooling.

## Algorithm choices

- Manhattan distance is admissible and consistent for cardinal grids with minimum move cost one.
- A binary min-heap makes A* frontier operations logarithmic and avoids linear scans.
- A supercover integer traversal was chosen for line of sight so grid cells touched by a segment are treated consistently.
- Utility AI was selected over a behavior tree because the portfolio goal includes transparent trade-offs, tunable weights, and direct decision explanations. The ADR records the alternatives.
- SplitMix64 was selected for a small explicit cross-platform seed stream. It is not used for cryptography.

## Secure delivery choices

- GitHub recommends pinning actions to full commit hashes and using minimum token permissions. Both workflows follow that guidance.
- No third-party NuGet package is required. Locked restore and a cleared package-source list keep the build graph small.
- Limits on maps, matches, turns, expansions, and JSON depth reduce accidental resource exhaustion from malformed inputs.

## Primary sources

- Unity 6 release overview: https://unity.com/releases/unity-6
- Unity 6000.3.13f1 release notes: https://unity.com/releases/editor/whats-new/6000.3.13f1
- .NET 10 overview: https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10/overview
- .NET support policy: https://learn.microsoft.com/dotnet/core/releases-and-support
- GitHub Actions secure use: https://docs.github.com/actions/security-guides/security-hardening-for-github-actions
