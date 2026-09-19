# Replay and save formats

## Versioning

Both formats use an integer `formatVersion`. Version 1 is current. The save reader also accepts a legacy version-zero state document without an envelope and migrates it in memory. Unknown future versions fail closed.

## Save shape

A save contains map dimensions, row-major cells, active team, turn number, and units with cooldowns. The reader validates dimensions, cell count, enum values, positive terrain costs, cover bounds, unique unit identifiers, walkable spawns, and non-negative cooldowns before returning state.

`scenarios/duel-v1.json` is both an authored scenario and a valid version-one save.

## Replay shape

A replay contains:

- its format version;
- a complete initial state;
- ordered commands;
- a 64-character expected state hash after each command.

Command fields are kind, actor, target coordinate, optional target unit, and optional ability. Replay files are append-only evidence; editing a command or expected hash causes validation to stop at the first mismatch.

## Canonical state hash

`StateHasher` includes map dimensions, active side, turn, every row-major terrain cell, every unit sorted by ordinal ID, and every cooldown sorted by ordinal ability ID. The UTF-8 canonical string is hashed with SHA-256.

The hash detects accidental or deliberate replay divergence. It is not a digital signature and does not prove authorship. A competitive networked product would authenticate replay files separately.
