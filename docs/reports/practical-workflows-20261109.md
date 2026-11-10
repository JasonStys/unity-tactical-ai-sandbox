# Offline game replay review validation â€” 2026-11-09

Author: Jason Stys. Baseline: `293d50bac6546d032e6af120f7810fa819532ce4`.

Added a bounded, read-only HTML/JSON inspection workflow to the existing fictional turn-based game. Every recorded action/hash is verified before export; initial/post-command frames include exact coordinates, health, cooldowns, terrain visibility and separately recomputed default-agent diagnostics. Labels distinguish replay hindsight and unknown recording weights. No simulation rules, AI weights or deterministic tournament results changed.

Local Windows validation (.NET SDK 10.0.403 selected by existing 10.0.401 / latestPatch policy; Node 24):

- `npm test`: locked restore, `dotnet format --verify-no-changes`, all five projects including Unity stub contract built with zero warnings/errors; **36/36 C# tests** passed. Independent new fixture expects blue x 1 â†’ 3 and red health 100 â†’ 60, checks every recorded state hash, no input mutation, tamper/129-command/budget rejection, terminal/no-suggestion, exhausted one-candidate search and inert hostile labels/oversized output.
- Existing **500-match byte-for-byte goldens remain identical**: 253 blue wins, 238 red wins, 9 draws, average 30.21 turns; replay 26 commands, final SHA `1b2e374e5e953299513e8f2b83fe854923eb6110e76d991c5637dad426e47ed7`. Save migration/validation and source-index/repository hygiene passed.
- `npm run test:e2e`: **8/8 actual browser journeys** passed in Chromium, Firefox, WebKit and mobile WebKit. The actual CLI exports a seed 42 match, refuses overwriting its existing HTML, exports hostile identifiers inertly, and browser checks validate sliderâ†’state hash/coordinates, avatar selection, exact JSON download, Axe accessibility, mobile overflow and zero HTTP(S) requests.
- Full `npm audit --audit-level=moderate`: **zero vulnerabilities** in the newly exact-locked browser-only tools. Viewer JS syntax check passed.

The old local verifier recursively removed the entire ignored `artifacts` tree. It now creates a unique owned subdirectory per validation and preserves prior/user files. Source indexing excludes generated bin/obj trees while adding authored viewer declarations. CI retains existing Linux/Windows/native/container/CodeQL gates and adds exact-lock full-graph audit plus real browser journeys on Linux, with immutable action pins and existing permissions.

The sandbox blocked .NET named-pipe build hosts; identical scoped local execution passed. No local Docker repair, remote push/Actions run, Unity Editor or manual screen-reader validation was performed in this dated local session. HTML is a local mutable review artifact, not signed evidence; native CSP blocks remote resources, but users who edit the artifact can alter it. Only fictional game coordinates are supported. Inspection limits and interpretation are detailed in `docs/REPLAY_REVIEW.md`.

## Linux browser follow-up

Hosted publication CI exposed a genuine Linux/font-dependent `scrollable-region-focusable` Axe violation on the avatar table wrapper. The exporter now gives that region an accessible name, `tabindex=0` and a visible focus outline. A new actual-export regression narrows the viewport to 240 px, tabs from avatar selection into the region, verifies real overflow, presses ArrowRight and observes horizontal movement before rerunning Axe. No rule is disabled. Local rerun: 36 C# checks/goldens and 12 browser journeys across all four projects passed; Linux hosted publication is validated separately.
