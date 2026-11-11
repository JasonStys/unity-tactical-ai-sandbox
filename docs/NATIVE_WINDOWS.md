# Native Windows designer

Made by Jason Stys · [GitHub/contact](https://github.com/JasonStys).

The Windows x64 application is a real WinForms editor over the existing C# game core and versioned serialization. It is separate from the optional Unity adapter: this work does not execute Unity Editor, PlayMode or a Unity-rendered player. There is no network service, external AI, live device or real-world targeting/control.

## Use the application

When an owner-reviewed native release is available, download its Windows ZIP and SHA-256 sidecar from this repository's releases. Verify the hash, extract the **whole directory** into a normal application folder, and open `TacticalAiDesigner.exe`. Keep every bundled runtime DLL and notice. The source candidate's CI artifacts are not yet a published or independently installed release. Packages are unsigned; do not bypass OS security warnings or enable administrator execution.

1. Open Scenario editor and choose a prepared encounter, or edit a complete custom scenario JSON. The text remains a draft until **Apply scenario JSON**. Applying a scenario starts a new recording and asks before discarding recorded work.
2. Select an initial board cell and configure walkability, movement cost and cover. Edits are refused once commands exist, and an occupied live-avatar cell cannot become a wall.
3. Select an active-team avatar and an action, or advance the real default bot with an explicit evaluation budget. Rejected actions do not advance time. Avatar labels and the table identify the same team, position, health and cooldowns.
4. Previous/Next and the slider inspect the complete validated recording. Return to the tail before appending commands. The next recorded action is hindsight; the default-bot suggestion is a separate recomputation, not an explanation of an unknown recording policy. Visibility is terrain-only, not proof of ability range.
5. Apply title and **Save project** to retain the complete project and selected frame. A second save retains the exact previous revision. Imports never silently save over current storage. Export project/replay/frame/inspection JSON or original self-contained review HTML to a new filename; existing files are refused.
6. Seeded balance runs original generated encounters for consecutive seeds. It does **not** simulate the custom edited board. The chosen cap limits executed actions, while the original `turns` telemetry is a one-based state counter. JSON/CSV show real completed cohort rows and accounting, not forecasts.

Help and an explicit Exit application button are in the real window. Changed drafts/title/accepted work prompt before discard. While an accepted operation is pending, repeated Quit/Close retains the visible owner and profile lock until it completes; errors remain visible. There is no forced process-kill/reset path.

[designer-duel-v1.json](../scenarios/designer-duel-v1.json) is a small prepared file for **Open JSON** in the actual application: move BlueA to (1,0), then let RedA use pulse-shot on BlueA. BlueA has 66 HP afterward; moving onto the living opponent is rejected. These are fictional game data, not a separate demo program.

## Durable private data

The default profile is under the current Windows user's LocalAppData, at `JasonStysPortfolio/unity-tactical-ai-sandbox/workspace`, outside Git and the application directory. Optional `--profile ABSOLUTE_DIRECTORY` selects another workspace. Unknown nonempty directories and wrong markers are refused, not reset. Keep profiles/exports private: this is one trusted OS owner's plaintext workspace, with no accounts, authenticated reviewers or remote-user authorization boundary.

Marker, lock, current and previous files are checked before initialization. UNC/device paths, network drives and alternate data streams are rejected before file metadata probes; documents and profiles require a fixed/removable local disk. Existing reparse/junction ancestors, hardlinked mutable members, install-tree profiles and non-ordinary inputs are rejected. Actual opened input descriptors are bounded, strict UTF-8 and single-link checked. These are static admission checks for accidental aliases, not atomic protection against a malicious concurrent writer using the same OS account. SHA-256 values detect changes relative to a held snapshot; they are not signatures or rollback authentication.

Malformed/current-corrupt storage is preserved. **Load previous** permits inspection/export; Save stays blocked until the user explicitly recovers into a new empty profile. No automatic reset, overwrite, directory deletion or hidden migration occurs. The unchanged version-1 save/replay formats and original legacy save import remain supported.

## Build and test source

Use the current .NET SDK 10.0.404, Node 24 and PowerShell 7.4+ in a project-scoped environment. These are development/build tools; packaged users do not need them. The original solution's package-free restore remains `NuGet.Config`; the Windows project uses `scripts/NuGet.Native.Config`, requiring signed official Microsoft framework/runtime packages with trusted author certificates. No global SDK installation is performed by repository scripts.

```powershell
npm ci --ignore-scripts
npm test
./scripts/package-native.ps1 -OutputRoot "$PWD/artifacts/my-new-native-build"
```

The output root must be new. Clean source is required; hosted builds also require exact checkout SHA. A developer may explicitly use `-AllowDirty` for a labeled local candidate; CI refuses that override. The packager verifies included .NET and WindowsDesktop 10.0.15 metadata and actual DLL bytes against restored vendor packages, carries real runtime licenses/notices, removes debug symbols, archives, safely streams into a fresh extraction, re-hashes every member, and starts the extracted EXE twice.

The explicit developer `--acceptance-root PATH --phase 1|2` mode requires a fresh marker-only synthetic root for its first phase and an existing successful first receipt for its second. It drives the same real visible native controls and ordinary storage handlers. Only file-dialog choices are predetermined. It does not open default profiles, replace engine outcomes, inject global keyboard/mouse events or bypass application permission checks. Ordinary application startup uses normal visible file dialogs.

The new native workflow stores bounded JSON/log/screenshot evidence by default. A full unsigned ZIP is retained only for an explicit trusted-main manual request, for one day. No workflow publishes a release or changes installed data. The original Linux/Windows tests, deterministic 500-match evidence and Unity contract build remain required. The original Ubuntu CodeQL job identity is retained; a separate actual Windows manual-build analysis extracts the WinForms client, studio, serialization and referenced core.

## Practical bounds

The designer accepts one MiB of strict UTF-8 JSON, board sides 2–32, 2–16 avatars, identifiers up to 64 characters, 128 recorded commands, and new exports up to eight MiB. The original CLI has its own documented bounds. Balance is 1–100 matches, 1–128 actions per match and nonwrapping consecutive seeds. Native compatibility is Windows x64 only; no macOS/Linux GUI, signed installer, clean-machine deployment, multiplayer, Unity scene integration or universal game-balance claim is made.
