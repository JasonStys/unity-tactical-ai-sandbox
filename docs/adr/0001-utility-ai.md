# ADR 0001: Use bounded utility AI for tactical decisions

- Status: Accepted
- Date: 2026-09-18

## Context

The sandbox needs opponents that react to damage, cover, range, threat, and defeat opportunities while remaining deterministic, explainable, testable, and inexpensive enough for large headless tournaments.

## Decision

Use a utility system that enumerates legal attack and movement candidates, computes integer scores from documented considerations, and selects the highest score with a stable command-key tie-break. Cap candidate evaluation per decision and return diagnostics with the command.

## Alternatives considered

### Behavior tree

Behavior trees visualize control flow well and are familiar to designers, but cross-branch tactical trade-offs become hidden in ordering and decorator thresholds. Deterministic scoring experiments and telemetry attribution would require additional machinery.

### Finite-state machine

A state machine is simple for patrol/chase/attack loops but scales poorly when cover, cooldown, kill probability, and multiple units interact. Transitions can become a second implicit scoring system.

### Minimax or Monte Carlo search

Lookahead can improve tactical quality, but it expands state copies and branching cost beyond this project's initial simulation budget. It would also make the first vertical slice harder to explain.

## Consequences

- Designers can tune weights and see an explanation of the selected action.
- Tests can assert exact stable decisions.
- Batch telemetry can reveal passive or dominant policies.
- Myopic behavior remains possible because the system does not simulate future turns.
- New considerations must use comparable scales and receive regression evidence.

## Revisit when

Add coordinated squad plans, objectives requiring multi-turn commitment, or opponent modeling. A hybrid could use a behavior tree for modes and utility scoring within each mode.
