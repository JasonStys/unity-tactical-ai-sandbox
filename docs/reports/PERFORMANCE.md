# Performance regression report

Date: 2026-09-18

## Budgets

| Workload | CI budget | Latest local observation |
| --- | ---: | ---: |
| 500 A* searches across a 64 × 64 empty map | under 10 seconds | under 1 second |
| 100 seeded bot matches, at most 100 commands each | under 20 seconds | under 1 second |
| Complete 32-test suite | no hard suite limit | approximately 1–2 seconds |

The individual limits are deliberately generous for shared GitHub runners. They are regression alarms, not benchmark claims. Build time, process startup, JSON writing, and repository validation are excluded from the two algorithm budgets.

## Complexity review

A* frontier operations are `O(log n)` and each reachable cell can update the best-cost table. Expected time is `O((V + E) log V)` with `O(V)` auxiliary storage. The explicit expansion cap prevents unbounded search.

Utility candidate enumeration is bounded by `evaluationBudget`. Influence calculation is `O(cells × enemySources)` and is appropriate for the small 12 × 8 generated boards. Larger boards or many sources should switch to multi-source graph propagation.
