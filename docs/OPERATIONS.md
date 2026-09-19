# Operations

## Local validation

```bash
npm test
```

The command deletes and recreates only the repository-local `artifacts/` directory. It does not modify checked-in evidence. A passing run ends with `complete validation gate passed`.

## Common commands

```bash
# Fast demo
dotnet run --project src/TacticalAI.Headless/TacticalAI.Headless.csproj -c Release -- demo

# Balance run
dotnet run --project src/TacticalAI.Headless/TacticalAI.Headless.csproj -c Release -- simulate --matches 500 --seed 1000 --out artifacts/simulation

# Replay verification
dotnet run --project src/TacticalAI.Headless/TacticalAI.Headless.csproj -c Release -- replay artifacts/simulation/sample-replay.json

# Scenario/save validation
dotnet run --project src/TacticalAI.Headless/TacticalAI.Headless.csproj -c Release -- validate-save scenarios/duel-v1.json

# Refresh declaration lines after source edits
node scripts/generate-code-index.mjs
```

## Updating deterministic evidence

Run the fixed 500-match command with output directed to `docs/reports/generated`, inspect the JSON/CSV changes, explain expected balance differences in `docs/reports/BALANCE.md`, refresh the code index, and run `npm test`. Never accept an evidence diff without identifying the rule or tuning change that caused it.

## Unity setup

1. Install Unity 6000.3.13f1 through Unity Hub.
2. Open the `unity/` directory as a project.
3. Build `TacticalAI.Core` for `netstandard2.1`.
4. Place or reference `TacticalAI.Core.dll` so the `TacticalAI.Runtime` assembly definition resolves it.
5. Create ability assets from **Create → Tactical AI → Ability**.
6. Add `TacticalSandboxController` to a GameObject and use its context menu.

The repository intentionally does not commit generated Unity `Library`, `Temp`, or user-setting data.

## Troubleshooting

| Symptom | Likely cause | Resolution |
| --- | --- | --- |
| Locked restore fails | Lock file changed or SDK differs | Use SDK 10.0.401+, run a normal restore intentionally, inspect lock diffs. |
| Generated evidence differs | Rules, weights, ordering, or runtime behavior changed | Compare the first differing seed and replay its commands. |
| Code index is stale | Declaration lines moved | Run `node scripts/generate-code-index.mjs`. |
| Unity cannot resolve core types | Portable DLL is not referenced | Build the `netstandard2.1` target and configure the assembly reference. |
| A match reaches the cap | Units cycled or could not produce a terminal state | Inspect its seed, action distribution, and final state hash; add a regression fixture. |
