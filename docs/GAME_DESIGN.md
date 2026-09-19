# Game and AI design

## Vertical slice

The included rules model a compact, turn-based skirmish. Each team chooses one living unit and takes one action: move up to three cells, use an ability, or wait. Victory occurs when only one team has living units. A hard turn limit converts non-terminal matches into draws during automated tournaments.

## Data-driven rules

An `AbilityDefinition` supplies a stable ID, base damage, Manhattan range, and cooldown. A `TerrainCell` supplies walkability, movement cost, and cover percentage. Unity's `AbilityAsset` is an authoring adapter that validates values by converting them into the same core definition used by tests and headless simulation.

Damage after cover is:

```text
max(1, floor(baseDamage × (100 - coverPercent) / 100))
```

Integer math avoids platform rounding differences.

## Utility scoring

Attacks score expected damage plus a large defeat bonus. Moves score cover and proximity while subtracting hostile influence. Stable command keys break ties after the score sort.

The default weights intentionally favor engagement:

| Consideration | Weight | Direction |
| --- | ---: | --- |
| Damage | 10 | Higher is better |
| Defeat | 1,000 | Strongly prefer removing a unit |
| Cover | 2 | Prefer safer destinations |
| Distance | 30 | Prefer closing with the nearest enemy |
| Threat | 1 | Avoid unnecessary exposure without causing paralysis |

The first large tournament found that a heavier threat penalty caused cautious movement loops. The current weights reduced draws to 9 of 500 seeded matches, and the suite now rejects a substantial disengagement regression.

## Designer feedback

`UtilityDecision.Explanation` reports the terms behind the selected action. `CandidatesEvaluated` and `BudgetExhausted` reveal workload pressure. JSON/CSV reports make outcome and action-distribution shifts visible after tuning.

## Extensibility

The next useful increments would be objectives, area effects, status effects, reaction fire, asymmetric unit roles, and a board/debug overlay. Those should enter as data and focused rule services rather than conditionals inside the Unity component.
