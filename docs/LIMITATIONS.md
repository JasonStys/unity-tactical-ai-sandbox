# Limitations and future work

The native designer is an unsigned self-contained **Windows x64** application over the actual original core. It does not run Unity Editor/PlayMode, install a service, provide accounts/multiplayer or support network filesystem profiles. Its own bounded JSON/scenario/history/cohort limits are in [NATIVE_WINDOWS.md](NATIVE_WINDOWS.md). Native CSV `commands` counts executed actions; original `turns` is the one-based game-state counter. Seeded balance uses generated encounters, not the custom editor board. Current source-candidate package proof does not imply a published, independently installed, signed or clean-machine-tested release.

- The checked-in Unity directory is an integration slice, not a complete playable scene or art package.
- CI compile-checks the Unity adapter but does not run Unity Edit Mode, Play Mode, rendering, input, or platform-build tests.
- The AI performs one-ply scoring; it does not search opponent responses or coordinate multi-unit plans.
- Influence mapping ignores terrain path cost and line-of-sight occlusion.
- A* supports rectangular cardinal grids only; no diagonal, navmesh, height, or destructible terrain model exists.
- The simulation gives each team one action per turn rather than using initiative points.
- Save migration demonstrates one legacy step; a production game should retain fixture coverage for every shipped format.
- Replay hashes detect divergence but are not cryptographic signatures or anti-cheat proofs.
- Performance checks are generous regression guards, not hardware-normalized benchmarks.
- Balance results cover the included abilities, weights, map generator, and seeds only; they do not establish universal fairness.
- JSON and CSV are written synchronously because the datasets are intentionally bounded.

High-value next steps are a Unity grid visualization, selectable debug overlays, objectives, multiple unit roles, status effects, replay scrubbing, golden rendered scenes, and licensed Unity test-runner automation.
