# Replay inspection workflow

Author: Jason Stys. Intended user: a fictional game designer/developer reviewing recorded behavior offline.

1. Run the existing seeded simulation, retaining `sample-replay.json` and balance results.
2. Run `inspect-replay FILE --out NEW.html`. A bad recorded action, hash mismatch, unsupported size or existing output fails explicitly. No partial review is returned for invalid replays.
3. Open the HTML, scrub the labelled frame slider and choose an avatar. Initial frame zero precedes commands; each later frame is the exact validated post-command state. The table, ordinal board labels and fingerprint make comparisons auditable.
4. Inspect the previous applied command, next recorded command (hindsight), and current default-bot diagnostic separately. An incomplete candidate search is visible. Terrain visibility is not an ability range/eligibility determination.
5. Download the review JSON for comparison. It contains defensive copies, schema version and the final validated hash; the browser does not rerun the engine or authenticate edited HTML/JSON.

`src/TacticalAI.Serialization/ReplayInspection.cs` validates and snapshots states, computes bounded diagnostics, embeds safe JSON and hashes fixed JS/CSS for CSP. `replay-viewer.js` uses text nodes and fixed SVG elements to render and export; `replay-viewer.css` supplies responsive high-contrast controls/table layout. `InspectionTests.cs` independently checks coordinates, damage, recorded hashes, terminal/search bounds and HTML escaping. `scripts/browser-review.mjs` creates isolated real exports and checks no-overwrite behavior; `tests/browser/review.spec.mjs` verifies browser navigation, downloads, accessibility, overflow and hostile labels. The Playwright configuration covers three engines and a mobile viewport.

The inspection limits are deliberately smaller than the engine's authoring limits: 128 commands, 16 avatars, 32×32 cells, move cost at most100, bounded identifiers/cooldowns, 1 MiB CLI input and 8 MiB generated JSON. Default-bot candidate budget is64 (service accepts1–256). No new AI policy, physics, live-control interface or real-world tracking/targeting is added. Current terrain is static. Unity integration remains compiled stub contract coverage, not Editor/PlayMode validation; automated Axe checks do not establish manual screen-reader usability.
