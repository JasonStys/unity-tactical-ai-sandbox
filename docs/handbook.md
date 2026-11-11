# Unity Tactical AI Sandbox handbook

By Jason Stys.

Current source candidate: [native Windows designer guide](NATIVE_WINDOWS.md) and [observed validation](reports/native-designer-2026-11-11.md). The actual WinForms application adds custom scenario/terrain authoring, legal turns, full replay/frame inspection, retained projects/previous revisions and original seeded-balance exports. Source-candidate proof, hosted checks and a published/installed release are different stages; the historical editions below retain their declared baseline.

Inspect hash-validated game replays with timeline scrubbing, terrain/line-of-sight state and independent default-bot diagnostics in bounded offline HTML.

Makes game behavior review portable while distinguishing recorded hindsight from independent replay decisions.

## Read the documentation

| Edition                                          | Audience and contents                                                                       |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------- |
| [project-guide.docx](reports/project-guide.docx) | User and developer guide: purpose, current usage, design, boundaries and source references. |

The editable Markdown remains the maintenance source. The Word files are readable editions, and the workbook is an evidence index rather than a test runner. See [publication identities and review](reports/publications.json) and the [documentation refresh record](reports/documentation-refresh-2026-11-09.md).

## Current usage and help

[README quick start](../README.md) is the entry point. Use the project-specific guides below before changing inputs, operating modes or deployment assumptions.

### Usage and operations

- [OPERATIONS](OPERATIONS.md)
- [REPLAY REVIEW](REPLAY_REVIEW.md)

### Design and contracts

- [ARCHITECTURE](ARCHITECTURE.md)
- [GAME DESIGN](GAME_DESIGN.md)
- [RESEARCH](RESEARCH.md)
- [adr/0001 utility ai](adr/0001-utility-ai.md)

### Security and validation

- [SECURITY](SECURITY.md)
- [TESTING](TESTING.md)

## Historical results and reproduction

The reviewed source is [`ea8c250a5112912d67461237788a9657e7eb705d`](https://github.com/JasonStys/unity-tactical-ai-sandbox/tree/ea8c250a5112912d67461237788a9657e7eb705d). The observed functional workflow baseline is `ea8c250a5112912d67461237788a9657e7eb705d`; those observations are recorded at `2026-11-10T05:25:40.628667+00:00`. They do not certify an unrun later revision.

- [Detailed recorded validation](reports/practical-workflows-20261109.md)
- [Current Actions](https://github.com/JasonStys/unity-tactical-ai-sandbox/actions) records later integration checks separately.
- Preserve dated measurements and follow the existing testing guide to reproduce them; a test-source inventory alone does not establish execution.

## Practical boundaries

- Fictional turn-based game only; no real-world tracking, targeting or live control.
- Default-bot diagnostics do not reconstruct the recording policy; the viewer does not rerun the engine or authenticate mutable files.
- Inputs are bounded to 128 commands, 16 avatars, 32x32 static cells, 1 MiB import and 8 MiB JSON.
- Unity stub compilation is not Unity Editor/PlayMode validation; the upgraded source is not a newly published native game build.

## Maintaining these editions

Update the editable usage/design/test documents first. Rebuild and visually review every Word page and workbook tab before replacing an edition; verify the exact binary digests and evidence source in publications.json. Keep account data, logs with secrets, installed-app state and private test stores out of documentation. Source and release assets retain their existing security and licensing boundaries.
